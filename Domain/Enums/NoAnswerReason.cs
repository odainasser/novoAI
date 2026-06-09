namespace Domain.Enums;

/// <summary>
/// Why an assistant turn produced no business answer. The five code-set reasons are
/// ALWAYS determined by code — the engine knows why a turn failed; the model never
/// guesses. (Empty results, permission and branch refusals are honest answers, not
/// no-answers, and are never recorded here.)
/// </summary>
public enum NoAnswerReason
{
    /// <summary>
    /// Reviewer-only decision (the engine never sets this): "this is a coverage gap that
    /// deserves a plan." Choosing it in the queue promotes the row into the plan-review
    /// flow (the plan editor opens, seeded from the cluster).
    /// </summary>
    NoPlanNoFallback = 1,

    /// <summary>The domain and entity are covered, but no plan matched and the model called no tool.</summary>
    NoCallingTool = 2,

    /// <summary>A required parameter could not be resolved/extracted (should have asked the user).</summary>
    MissingParameter = 3,

    /// <summary>The question's business domain is not covered by any tool (needs a new capability).</summary>
    UnsupportedDomain = 4,

    /// <summary>The question's entity is not covered by any tool (needs a new tool for it).</summary>
    UnsupportedEntity = 5,

    /// <summary>Timeout, tool exception, or the leak-guard fallback fired (operational).</summary>
    Error = 6
}
