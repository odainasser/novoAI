namespace Application.Features.Assistant;

/// <summary>
/// Records assistant turns for the admin "plan" page and supplies confirmed plans
/// back to the model as few-shot supervision. Best-effort: it must NEVER block or
/// break the answer pipeline.
/// </summary>
public interface IAssistantLearningService
{
    /// <summary>Logs a turn; returns the created interaction id (Guid.Empty on failure).</summary>
    Task<Guid> RecordInteractionAsync(
        Guid appId,
        string question,
        string locale,
        IReadOnlyList<string> toolsUsed,
        bool answered,
        bool isMixing,
        string answer,
        Guid? branchId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a no-answer turn into its review cluster (reason + normalized question),
    /// incrementing frequency. The reason is always code-set; this only persists it.
    /// Best-effort — never blocks the answer pipeline.
    /// </summary>
    Task RecordNoAnswerAsync(
        Guid appId,
        string question,
        string locale,
        Domain.Enums.NoAnswerReason reason,
        string evidence,
        Guid? branchId,
        string? userFacingMessage,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a user-reported answer as an immutable snapshot (copy) for the admin
    /// "reported answers" queue. Best-effort — never blocks the report request.
    /// </summary>
    Task RecordReportedAnswerAsync(
        Guid appId,
        string question,
        string answer,
        string? feedback,
        string locale,
        Guid? branchId,
        string? reportedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Recent reviewer-confirmed plans (question → tools) to steer tool selection,
    /// used as few-shot examples in the system prompt. Returns an empty list on any
    /// error so phrasing is never blocked.
    /// </summary>
    Task<IReadOnlyList<ConfirmedPlanExample>> GetConfirmedPlanExamplesAsync(
        Guid appId, int max, CancellationToken cancellationToken = default);
}

/// <summary>A confirmed question → tools mapping used as a few-shot example.</summary>
public sealed record ConfirmedPlanExample(string Question, IReadOnlyList<string> Tools);
