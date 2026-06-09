namespace Infrastructure.Services.Assistant;

/// <summary>
/// The registry of every read tool the assistant can use. Singleton — the tool set
/// is code-owned and fixed at build time (consistent with "the application owns
/// every query decision"). The catalog also decides which tools to OFFER a given
/// caller: only those whose permissions the caller holds and, under a Branch-Panel
/// lock, only those that can be scoped to a single branch. The orchestrator
/// re-checks the same rules before executing, so filtering here is a usability
/// optimisation, not the security boundary.
/// </summary>
internal sealed class ToolCatalog
{
    private readonly Dictionary<string, IAssistantTool> _byName;

    public ToolCatalog()
    {
        var tools = ToolDefinitions.BuildAll();
        _byName = tools.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
    }

    public IAssistantTool? Find(string name) =>
        _byName.TryGetValue(name, out var t) ? t : null;

    /// <summary>Every registered tool — the source for the admin "plan" dropdowns.</summary>
    public IReadOnlyCollection<IAssistantTool> All => _byName.Values;

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
        _byName.Values.Where(t => CanUse(t, ctx)).ToList();

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
