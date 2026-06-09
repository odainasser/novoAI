namespace Domain.Enums;

/// <summary>Lifecycle of a stored assistant <see cref="Domain.Entities.AssistantPlan"/>.</summary>
public enum PlanStatus
{
    /// <summary>Authored but not yet approved for execution.</summary>
    Draft = 1,
    /// <summary>Approved — eligible to be matched and executed at runtime.</summary>
    Confirmed = 2,
    /// <summary>Retired — kept for audit/rollback but never matched.</summary>
    Deprecated = 3
}
