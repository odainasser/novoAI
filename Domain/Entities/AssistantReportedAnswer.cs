using Domain.Common;

namespace Domain.Entities;

/// <summary>
/// A user-reported assistant answer. This is a COPY (snapshot) of the reported turn —
/// the question and the exact answer text the user saw — captured at report time and
/// kept independent of the user's live chat history (which is never altered or moved).
/// It carries an optional free-text <see cref="Feedback"/> from the reporter and a
/// simple review lifecycle so a reviewer can mark it resolved.
/// </summary>
public class AssistantReportedAnswer : BaseAuditableEntity
{
    /// <summary>The registered app this report belongs to.</summary>
    public Guid? AppId { get; set; }

    /// <summary>Snapshot of the question that produced the reported answer.</summary>
    public string Question { get; set; } = string.Empty;

    /// <summary>Snapshot of the answer the user reported (copied, not moved).</summary>
    public string Answer { get; set; } = string.Empty;

    /// <summary>Optional free-text feedback the reporter typed in the report modal.</summary>
    public string? Feedback { get; set; }

    public string Locale { get; set; } = "en";

    /// <summary>The branch the assistant was scoped to (Branch Panel), if any.</summary>
    public Guid? BranchId { get; set; }

    /// <summary>The reporting user's id (NameIdentifier), for follow-up.</summary>
    public string? ReportedBy { get; set; }

    /// <summary>Set when a reviewer has handled the report.</summary>
    public bool Resolved { get; set; }

    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedBy { get; set; }
}
