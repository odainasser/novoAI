using System.Collections.Concurrent;
using Domain.Entities;
using Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services.Assistant;

/// <summary>
/// Per-app registries of the read tools the assistant can use. Tools are owned by
/// the registered apps (the systems that own the data): for each app this catalog
/// fetches the tool descriptors from the app's /api/assistant-data surface, caches
/// a snapshot, and wraps each descriptor in a tool whose execution is a remote call
/// under the calling user's own token. The catalog still decides which tools to
/// OFFER a given caller: only those whose permissions the caller holds and, under a
/// Branch-Panel lock, only those that can be scoped to a single branch. The app
/// re-checks the same rules before executing, so filtering here is a usability
/// optimisation, not the security boundary.
/// </summary>
internal sealed class ToolCatalog
{
    private sealed record Snapshot(Dictionary<string, IAssistantTool> ByName, DateTime LoadedAtUtc);

    private readonly AppToolsClient _appTools;
    private readonly AppsIntegrationSettings _settings;
    private readonly ILogger<ToolCatalog> _logger;

    private readonly ConcurrentDictionary<Guid, Snapshot> _byApp = new();
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _refreshLocks = new();

    public ToolCatalog(
        AppToolsClient appTools,
        IOptions<AppsIntegrationSettings> options,
        ILogger<ToolCatalog> logger)
    {
        _appTools = appTools;
        _settings = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Make sure a usable tool snapshot is loaded for the app (fetch/refresh from its
    /// BaseUrl). Throws when no snapshot can be obtained at all — the orchestrator
    /// turns that into its deterministic "data unavailable" answer. A failed REFRESH
    /// keeps serving the stale snapshot instead of failing the turn.
    /// </summary>
    public async Task EnsureLoadedAsync(App app, CancellationToken ct)
    {
        if (IsFresh(app.Id)) return;

        var refreshLock = _refreshLocks.GetOrAdd(app.Id, _ => new SemaphoreSlim(1, 1));
        await refreshLock.WaitAsync(ct);
        try
        {
            if (IsFresh(app.Id)) return;

            try
            {
                var descriptors = await _appTools.GetCatalogAsync(app.BaseUrl, ct);
                var byName = descriptors
                    .Select(d => BuildTool(app, d))
                    .Cast<IAssistantTool>()
                    .ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
                _byApp[app.Id] = new Snapshot(byName, DateTime.UtcNow);
                _logger.LogInformation("Tool catalog loaded for app '{App}' ({Count} tools).", app.Code, byName.Count);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                if (!_byApp.TryGetValue(app.Id, out var stale))
                    throw;
                // Serve stale rather than fail the turn; back off a full TTL before retrying.
                _logger.LogWarning(ex, "Tool catalog refresh failed for app '{App}'; serving the stale snapshot.", app.Code);
                _byApp[app.Id] = stale with { LoadedAtUtc = DateTime.UtcNow };
            }
        }
        finally
        {
            refreshLock.Release();
        }
    }

    private bool IsFresh(Guid appId) =>
        _byApp.TryGetValue(appId, out var s)
        && (DateTime.UtcNow - s.LoadedAtUtc).TotalSeconds < Math.Max(30, _settings.CatalogCacheSeconds);

    private DelegateTool BuildTool(App app, AppToolDescriptor d)
    {
        var baseUrl = app.BaseUrl;
        var tool = new DelegateTool(
            d.Name,
            d.Description,
            exec: async (args, ctx) =>
            {
                var result = await _appTools.ExecuteAsync(baseUrl, d.Name, args, ctx.BranchId, ctx.Locale, ctx.Ct);
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

    public IAssistantTool? Find(Guid appId, string name) =>
        _byApp.TryGetValue(appId, out var s) && s.ByName.TryGetValue(name, out var t) ? t : null;

    /// <summary>Every registered tool of one app — the source for the admin "plan" dropdowns.</summary>
    public IReadOnlyCollection<IAssistantTool> All(Guid appId) =>
        _byApp.TryGetValue(appId, out var s)
            ? (IReadOnlyCollection<IAssistantTool>)s.ByName.Values
            : Array.Empty<IAssistantTool>();

    /// <summary>The app's tool names in the current snapshot (leak-guard scan list).</summary>
    public IReadOnlyCollection<string> ToolNames(Guid appId) =>
        _byApp.TryGetValue(appId, out var s)
            ? (IReadOnlyCollection<string>)s.ByName.Keys
            : Array.Empty<string>();

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
        All(ctx.App.Id).Where(t => CanUse(t, ctx)).ToList();

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
