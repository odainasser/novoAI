using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// A clustered no-answer entry in the review queue. Each row groups many phrasings of
/// the same uncovered need (or the same mis-refusal) under one <see cref="ClusterKey"/>
/// = (reason + normalized question), so the reviewer reviews one cluster and resolves
/// all its turns with one action.
///
/// The <see cref="Reason"/> is always code-set (the engine knows why the turn failed);
/// the reviewer can OVERRIDE it via <see cref="ReviewedReason"/>, which re-routes the
/// triage. <see cref="Evidence"/> is the proof code used to set the reason — required,
/// so the reviewer can confirm the reason was correct.
///
/// No answer data values and no identity beyond <see cref="BranchId"/> are stored.
/// </summary>
public class AssistantNoAnswer : BaseAuditableEntity
{
    /// <summary>The code-set reason this turn produced no answer.</summary>
    public NoAnswerReason Reason { get; set; }

    /// <summary>A reviewer override of <see cref="Reason"/> (e.g. a missed classification match).</summary>
    public NoAnswerReason? ReviewedReason { get; set; }

    /// <summary>Normalized/placeholdered question used to cluster phrasings.</summary>
    public string NormalizedQuestion { get; set; } = string.Empty;

    /// <summary>Unique cluster key: "reason|normalizedQuestion".</summary>
    public string ClusterKey { get; set; } = string.Empty;

    /// <summary>A representative original question (review context).</summary>
    public string SampleQuestion { get; set; } = string.Empty;

    public string Locale { get; set; } = "en";

    /// <summary>The proof code used to set the reason (classify result, failed gate, missing param…).</summary>
    public string Evidence { get; set; } = string.Empty;

    public Guid? BranchId { get; set; }

    /// <summary>How many turns fell into this cluster (rank the queue by this).</summary>
    public int Frequency { get; set; } = 1;

    /// <summary>The message the user actually saw (so the reviewer sees what was returned).</summary>
    public string? UserFacingMessage { get; set; }

    /// <summary>A representative interaction id, so "create a plan" can act on a real turn.</summary>
    public Guid? SampleInteractionId { get; set; }

    public DateTime LastSeenAt { get; set; }

    /// <summary>Set when a reviewer confirms/corrects the reason (no status lifecycle).</summary>
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedBy { get; set; }
}
