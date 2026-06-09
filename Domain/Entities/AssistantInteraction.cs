using Domain.Common;

namespace Domain.Entities;

/// <summary>
/// One assistant turn, shown on the admin "plan" page. It captures the question,
/// the answer, and the plan the assistant actually executed — the tools it called,
/// from which the module(domain) and entities are derived in code. A reviewer can
/// CORRECT the plan (choose the right tools/domain/entities) and CONFIRM it; a
/// confirmed plan is kept as human supervision and fed back to the model as a
/// few-shot example to improve tool selection. Nothing is auto-applied, and the
/// stored answer is never reused as a response — data is always re-fetched live.
/// </summary>
public class AssistantInteraction : BaseEntity
{
    public string Question { get; set; } = string.Empty;
    public string Locale { get; set; } = "en";

    // ── The plan the assistant executed ───────────────────────────────
    /// <summary>Comma-separated names of the tools the model invoked (empty if none).</summary>
    public string? ToolsUsed { get; set; }

    /// <summary>True when at least one tool ran and produced a grounded answer.</summary>
    public bool Answered { get; set; }

    /// <summary>True when the turn combined two datasets (a mixing / multi-data answer).</summary>
    public bool IsMixing { get; set; }

    /// <summary>The answer text — review context only, never reused as a response.</summary>
    public string? Answer { get; set; }

    /// <summary>Active branch the turn ran under, if branch-locked (Branch Panel).</summary>
    public Guid? BranchId { get; set; }

    // ── The corrected/confirmed plan (human supervision; few-shot source) ──
    /// <summary>Reviewer-confirmed tool names (comma-separated).</summary>
    public string? ConfirmedTools { get; set; }

    /// <summary>Reviewer-confirmed module/domain.</summary>
    public string? ConfirmedDomain { get; set; }

    /// <summary>Reviewer-confirmed entities (comma-separated).</summary>
    public string? ConfirmedEntities { get; set; }

    /// <summary>True once a reviewer has corrected and confirmed the plan.</summary>
    public bool PlanConfirmed { get; set; }

    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedBy { get; set; }
}
