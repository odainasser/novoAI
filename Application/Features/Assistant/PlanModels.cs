namespace Application.Features.Assistant;

// ── The executable recipe (serialised into AssistantPlan.DefinitionJson) ──

/// <summary>The full executable definition of a plan (sections 2–6 of the schema).</summary>
public sealed class PlanDefinition
{
    /// <summary>Ordered tools to call (one for most plans, two+ for mixing).</summary>
    public List<PlanTool> Tools { get; set; } = new();

    /// <summary>Ordered join steps; empty for single-tool plans.</summary>
    public List<PlanJoin> Joins { get; set; } = new();

    public PlanOutput Output { get; set; } = new();

    /// <summary>Read permission(s) gated before execution (both entities for mixing).</summary>
    public List<string> RequiredPermissions { get; set; } = new();
}

public sealed class PlanTool
{
    /// <summary>Stable id used by joins/params (e.g. "t1").</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The code-owned tool/service name to execute.</summary>
    public string Name { get; set; } = string.Empty;

    public List<PlanParam> Params { get; set; } = new();
}

public sealed class PlanParam
{
    public string Name { get; set; } = string.Empty;

    /// <summary>static | extract | context.</summary>
    public string Source { get; set; } = "static";

    /// <summary>The fixed value (source = static).</summary>
    public string? Value { get; set; }

    /// <summary>The placeholder to pull from the question (source = extract), e.g. "{client}".</summary>
    public string? Placeholder { get; set; }

    /// <summary>The request/JWT key to read (source = context), e.g. "branchId". Never the question.</summary>
    public string? ContextKey { get; set; }

    /// <summary>
    /// When true, an unresolved extract value makes the turn ask the user (MissingParameter)
    /// rather than run unfiltered. Set by the no-answer triage "mark required" action.
    /// </summary>
    public bool Required { get; set; }
}

public sealed class PlanJoin
{
    /// <summary>Tool id of the left dataset.</summary>
    public string Left { get; set; } = string.Empty;

    /// <summary>Tool id of the right dataset.</summary>
    public string Right { get; set; } = string.Empty;

    /// <summary>One or more key pairs (supports composite/chained joins).</summary>
    public List<PlanJoinKey> On { get; set; } = new();
}

public sealed class PlanJoinKey
{
    public string LeftKey { get; set; } = string.Empty;
    public string RightKey { get; set; } = string.Empty;
}

public sealed class PlanOutput
{
    /// <summary>Totals code must compute and inject, e.g. "count", "sum:amountDue".</summary>
    public List<string> PrecomputeTotals { get; set; } = new();

    /// <summary>Max rows passed to the phraser.</summary>
    public int? RowCap { get; set; }

    /// <summary>Per-entity deep-link route pattern, e.g. { "Invoice": "/admin/invoices/{id}" }.</summary>
    public Dictionary<string, string> LinkRoute { get; set; } = new();

    /// <summary>If set and the output is simple, render via template (0 LLM calls).</summary>
    public string? TemplateId { get; set; }
}

// ── Matching key (section 1) — produced by Call 1, matched against plans ──

/// <summary>The lightweight classification of a question used to find a plan.</summary>
public sealed class PlanMatch
{
    public List<string> Domains { get; set; } = new();
    public string? Action { get; set; }
    public string? Entity { get; set; }
    public string? SecondaryEntity { get; set; }

    public bool IsUsable => Domains.Count > 0 && !string.IsNullOrWhiteSpace(Entity);

    /// <summary>Normalised lookup key: "domain+domain|action|entity|secondary" (lower-cased).</summary>
    public string Key()
    {
        var domains = string.Join("+", Domains
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Select(d => d.Trim().ToLowerInvariant())
            .OrderBy(d => d));
        var action = (Action ?? "").Trim().ToLowerInvariant();
        var entity = (Entity ?? "").Trim().ToLowerInvariant();
        var secondary = (SecondaryEntity ?? "").Trim().ToLowerInvariant();
        return $"{domains}|{action}|{entity}|{secondary}";
    }
}
