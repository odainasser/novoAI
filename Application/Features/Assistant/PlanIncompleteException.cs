namespace Application.Features.Assistant;

/// <summary>
/// Thrown when a plan can't be confirmed because a tool parameter has no explicit
/// source decision (the completeness gate). Surfaced to the client as a 400 so the
/// reviewer sees exactly which parameter is undecided.
/// </summary>
public class PlanIncompleteException : Exception
{
    public PlanIncompleteException(string message) : base(message) { }
}
