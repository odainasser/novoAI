using System.Text.Json;

namespace Infrastructure.Services.Assistant;

/// <summary>
/// A single read-only capability the assistant model may invoke (an Ollama
/// "function"/tool). The model only chooses the tool and its arguments; the
/// application executes it. Every tool is gated by <see cref="Permissions"/> and,
/// when the caller is branch-locked, by <see cref="CrossBranch"/> — the catalog
/// never even offers a tool the caller can't use, and the orchestrator re-checks
/// before executing. The executor returns business data the model then phrases.
/// </summary>
internal interface IAssistantTool
{
    /// <summary>Snake-case function name the model calls (e.g. "get_revenue").</summary>
    string Name { get; }

    /// <summary>Plain-English description the model reads to decide when to call it.</summary>
    string Description { get; }

    /// <summary>All permissions required to use the tool. Empty = no permission needed.</summary>
    IReadOnlyList<string> Permissions { get; }

    /// <summary>The business module/domain this tool belongs to (e.g. "Sales"). Code-owned.</summary>
    string Domain { get; }

    /// <summary>The domain entities this tool reads (e.g. "Order", "Product"). Code-owned.</summary>
    IReadOnlyList<string> Entities { get; }

    /// <summary>
    /// True when the tool spans the whole company and cannot be scoped to one
    /// branch. Such tools are withheld (and refused) under a Branch-Panel lock.
    /// </summary>
    bool CrossBranch { get; }

    /// <summary>True when the tool combines two datasets in code (a mixing tool).</summary>
    bool IsMixing { get; }

    /// <summary>JSON-schema object for the function's parameters (Ollama "parameters").</summary>
    object ParametersSchema { get; }

    Task<ToolResult> ExecuteAsync(JsonElement arguments, ToolContext ctx);
}

/// <summary>
/// Per-turn execution context handed to a tool. Identity and branch scope come
/// from the authenticated request — never from model-supplied arguments.
/// <see cref="BranchWarehouseIds"/> is the active branch's warehouses (resolved
/// once by the orchestrator) when branch-locked; null otherwise.
/// </summary>
internal sealed record ToolContext(
    IServiceProvider Sp,
    string UserId,
    ISet<string> Permissions,
    Guid? BranchId,
    IReadOnlyList<Guid>? BranchWarehouseIds,
    string Locale,
    CancellationToken Ct)
{
    public bool BranchLocked => BranchId.HasValue;
    public Guid? UserGuid => Guid.TryParse(UserId, out var g) ? g : null;
    public Guid? WarehouseId => BranchWarehouseIds is { Count: 1 } ? BranchWarehouseIds[0] : null;
}

/// <summary>The data a tool produces (shaped, ID-free) for the model to phrase.</summary>
internal sealed record ToolResult(object? Data);

/// <summary>
/// Concrete tool built from delegates so the whole catalog can be declared in one
/// place without a class per capability.
/// </summary>
internal sealed class DelegateTool : IAssistantTool
{
    private readonly Func<JsonElement, ToolContext, Task<ToolResult>> _exec;

    public DelegateTool(
        string name,
        string description,
        Func<JsonElement, ToolContext, Task<ToolResult>> exec,
        string[]? permissions = null,
        object? parametersSchema = null,
        bool crossBranch = false,
        bool isMixing = false)
    {
        Name = name;
        Description = description;
        _exec = exec;
        Permissions = permissions ?? Array.Empty<string>();
        ParametersSchema = parametersSchema ?? ToolArgs.NoParams;
        CrossBranch = crossBranch;
        IsMixing = isMixing;
    }

    public string Name { get; }
    public string Description { get; }
    public IReadOnlyList<string> Permissions { get; }
    public bool CrossBranch { get; }
    public bool IsMixing { get; }
    public object ParametersSchema { get; }

    // Set once, after construction, from the tool descriptor fetched from the provider.
    public string Domain { get; set; } = string.Empty;
    public IReadOnlyList<string> Entities { get; set; } = Array.Empty<string>();

    public Task<ToolResult> ExecuteAsync(JsonElement arguments, ToolContext ctx) => _exec(arguments, ctx);
}

/// <summary>One parameter a tool accepts, read back from its JSON schema so the
/// admin UI can require an explicit source decision for each.</summary>
internal sealed record ToolParamSpec(
    string Name, string Type, IReadOnlyList<string> Enum, string? Description, bool Required);

/// <summary>Reads the structured parameter list out of a tool's JSON-schema object.</summary>
internal static class ToolSchema
{
    private static readonly System.Text.Json.JsonSerializerOptions Opts =
        new() { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };

    public static IReadOnlyList<ToolParamSpec> Params(object schema)
    {
        var list = new List<ToolParamSpec>();
        if (System.Text.Json.JsonSerializer.SerializeToNode(schema, Opts)
                is not System.Text.Json.Nodes.JsonObject root)
            return list;

        var required = (root["required"] as System.Text.Json.Nodes.JsonArray)?
            .Select(x => x?.ToString()).Where(x => !string.IsNullOrEmpty(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new();

        if (root["properties"] is System.Text.Json.Nodes.JsonObject props)
            foreach (var kvp in props)
            {
                var p = kvp.Value as System.Text.Json.Nodes.JsonObject;
                var type = p?["type"]?.ToString() ?? "string";
                var en = (p?["enum"] as System.Text.Json.Nodes.JsonArray)?
                    .Select(x => x?.ToString() ?? "").Where(x => x.Length > 0).ToList() ?? new();
                var desc = p?["description"]?.ToString();
                list.Add(new ToolParamSpec(kvp.Key, type, en, desc, required.Contains(kvp.Key)));
            }
        return list;
    }
}

/// <summary>Helpers for reading tool arguments and declaring parameter schemas.</summary>
internal static class ToolArgs
{
    /// <summary>An empty JSON-schema object (a tool that takes no arguments).</summary>
    public static readonly object NoParams = new { type = "object", properties = new { } };

    public static string? Str(JsonElement args, string name)
    {
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(name, out var v))
            return null;
        var s = v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number => v.ToString(),
            _ => null
        };
        s = s?.Trim();
        return string.IsNullOrWhiteSpace(s) || s.Equals("null", StringComparison.OrdinalIgnoreCase) ? null : s;
    }

    public static int? Int(JsonElement args, string name)
    {
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(name, out var v))
            return null;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n)) return n;
        if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out var s)) return s;
        return null;
    }

    public static bool? Bool(JsonElement args, string name)
    {
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(name, out var v))
            return null;
        return v.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(v.GetString(), out var b) => b,
            _ => null
        };
    }
}
