using Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services.Assistant;

/// <summary>
/// The registry of every read tool the assistant can use. The tool set is owned by
/// ByteMart (the system that owns the data): this catalog fetches the tool
/// descriptors from ByteMart's /api/assistant-data surface, caches a snapshot, and
/// wraps each descriptor in a tool whose execution is a remote call under the
/// calling user's own token. The catalog still decides which tools to OFFER a given
/// caller: only those whose permissions the caller holds and, under a Branch-Panel
/// lock, only those that can be scoped to a single branch. ByteMart re-checks the
/// same rules before executing, so filtering here is a usability optimisation, not
/// the security boundary.
/// </summary>
internal sealed class ToolCatalog
{
    private readonly MartToolsClient _mart;
    private readonly MartIntegrationSettings _settings;
    private readonly ILogger<ToolCatalog> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private volatile Dictionary<string, IAssistantTool>? _byName;
    private DateTime _loadedAtUtc = DateTime.MinValue;

    public ToolCatalog(
        MartToolsClient mart,
        IOptions<MartIntegrationSettings> options,
        ILogger<ToolCatalog> logger)
    {
        _mart = mart;
        _settings = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Make sure a usable tool snapshot is loaded (fetch/refresh from ByteMart).
    /// Throws when no snapshot can be obtained at all — the orchestrator turns that
    /// into its deterministic "data unavailable" answer. A failed REFRESH keeps
    /// serving the stale snapshot instead of failing the turn.
    /// </summary>
    public async Task EnsureLoadedAsync(CancellationToken ct)
    {
        if (IsFresh) return;

        await _refreshLock.WaitAsync(ct);
        try
        {
            if (IsFresh) return;

            try
            {
                var descriptors = await _mart.GetCatalogAsync(ct);
                _byName = descriptors
                    .Select(BuildTool)
                    .Cast<IAssistantTool>()
                    .ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
                _loadedAtUtc = DateTime.UtcNow;
                _logger.LogInformation("Assistant tool catalog loaded from ByteMart ({Count} tools).", _byName.Count);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                if (_byName is null)
                    throw;
                // Serve stale rather than fail the turn; back off a full TTL before retrying.
                _logger.LogWarning(ex, "Assistant tool catalog refresh failed; serving the stale snapshot.");
                _loadedAtUtc = DateTime.UtcNow;
            }
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private bool IsFresh =>
        _byName is not null
        && (DateTime.UtcNow - _loadedAtUtc).TotalSeconds < Math.Max(30, _settings.CatalogCacheSeconds);

    private DelegateTool BuildTool(MartToolDescriptor d)
    {
        var tool = new DelegateTool(
            d.Name,
            d.Description,
            exec: async (args, ctx) =>
            {
                var result = await _mart.ExecuteAsync(d.Name, args, ctx.BranchId, ctx.Locale, ctx.Ct);
                if (!string.Equals(result.Status, "ok", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Remote tool '{d.Name}' returned status '{result.Status}'.");
                return new ToolResult(result.Data.HasValue ? result.Data.Value : null);
            },
            permissions: d.Permissions.ToArray(),
            parametersSchema: d.ParametersSchema,
            crossBranch: d.CrossBranch,
            isMixing: d.IsMixing)
        {
            Domain = d.Domain,
            Entities = d.Entities
        };
        return tool;
    }

    public IAssistantTool? Find(string name)
    {
        var snapshot = _byName;
        return snapshot is not null && snapshot.TryGetValue(name, out var t) ? t : null;
    }

    /// <summary>Every registered tool — the source for the admin "plan" dropdowns.</summary>
    public IReadOnlyCollection<IAssistantTool> All =>
        (IReadOnlyCollection<IAssistantTool>?)_byName?.Values ?? Array.Empty<IAssistantTool>();

    /// <summary>The tool names in the current snapshot (leak-guard scan list).</summary>
    public IReadOnlyCollection<string> ToolNames =>
        (IReadOnlyCollection<string>?)_byName?.Keys ?? Array.Empty<string>();

    /// <summary>True when the caller may use the tool (holds all its permissions and
    /// the tool is offerable in the caller's branch context).</summary>
    public static bool CanUse(IAssistantTool tool, ToolContext ctx)
    {
        if (ctx.BranchLocked && tool.CrossBranch)
            return false;
        foreach (var p in tool.Permissions)
            if (!ctx.Permissions.Contains(p))
                return false;
        return true;
    }

    /// <summary>The tools to offer this caller (permission- and branch-filtered).</summary>
    public IReadOnlyList<IAssistantTool> Available(ToolContext ctx) =>
        All.Where(t => CanUse(t, ctx)).ToList();

    /// <summary>
    /// Build the Ollama "tools" array (function specs) for the offered tools.
    /// </summary>
    public static List<object> ToOllamaTools(IReadOnlyList<IAssistantTool> tools) =>
        tools.Select(t => (object)new
        {
            type = "function",
            function = new
            {
                name = t.Name,
                description = t.Description,
                parameters = t.ParametersSchema
            }
        }).ToList();
}
