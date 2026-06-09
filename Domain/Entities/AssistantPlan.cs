using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// A stored, governed assistant plan: the deterministic recipe for answering a
/// class of question. A lightweight classification of the question (domains, action,
/// entity, optional secondary entity) forms the lookup key; when a confirmed plan
/// matches, the engine executes its tools + parameters + joins and shapes the output
/// per the plan — the model never decides what to fetch or does cross-record math.
///
/// The match key is stored as queryable columns (+ a normalised <see cref="MatchKey"/>);
/// the executable recipe (tools, parameters, joins, output hints, required permissions)
/// is stored as JSON in <see cref="DefinitionJson"/>. Identity/tenant/branch are NEVER
/// stored on a plan — they always come from the authenticated request at execution.
/// </summary>
public class AssistantPlan : BaseAuditableEntity
{
    // ── 1. Identity / matching ────────────────────────────────────────
    /// <summary>One or two domains, comma-separated, normalised (lower, sorted).</summary>
    public string MatchDomains { get; set; } = string.Empty;

    /// <summary>count | sum | list | compare | top | detail | status.</summary>
    public string Action { get; set; } = string.Empty;

    public string Entity { get; set; } = string.Empty;

    /// <summary>Present only for mixing (two-entity) plans.</summary>
    public string? SecondaryEntity { get; set; }

    /// <summary>Normalised lookup key: "domains|action|entity|secondary" (lower-cased).</summary>
    public string MatchKey { get; set; } = string.Empty;

    // ── 2–6. The executable recipe (serialised PlanDefinition) ────────
    /// <summary>JSON: tools, per-tool parameters, joins, output hints, required permissions.</summary>
    public string DefinitionJson { get; set; } = "{}";

    /// <summary>A representative question (review context; not used for matching).</summary>
    public string? SampleQuestion { get; set; }

    public string? Locale { get; set; }

    // ── 7. Governance / lifecycle ─────────────────────────────────────
    public PlanStatus Status { get; set; } = PlanStatus.Draft;

    /// <summary>Bumped on each confirmed change so a plan can be rolled back.</summary>
    public int Version { get; set; } = 1;

    public string? ConfirmedBy { get; set; }
    public DateTime? ConfirmedAt { get; set; }

    /// <summary>How many times this plan has been executed (spot misfiring/graduation).</summary>
    public int UsageCount { get; set; }

    /// <summary>Rolling success signal in [0,1] (e.g. answered-and-not-corrected).</summary>
    public double SuccessScore { get; set; }
}
