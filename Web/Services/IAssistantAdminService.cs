using Web.Models.Assistant;
using Web.Models.Common;

namespace Web.Services;

/// <summary>
/// Web client for the tool-calling assistant's single "plan" admin page.
/// </summary>
public interface IAssistantAdminService
{
    Task<PaginatedList<AssistantInteractionDto>> GetInteractionsAsync(
        int pageNumber, int pageSize, string? search = null,
        bool? unansweredOnly = null, bool? confirmedOnly = null, Guid? appId = null);

    /// <summary>Registered apps (id + name) for the review-page app filter.</summary>
    Task<List<AppOptionDto>> GetAppOptionsAsync();

    Task<AssistantPlanOptionsDto> GetPlanOptionsAsync();

    Task ConfirmPlanAsync(Guid id, ConfirmAssistantPlanRequest request);

    Task<AssistantInteractionDto?> GetInteractionAsync(Guid id);

    // ── No-answer review queue ─────────────────────────────────────────
    Task<PaginatedList<NoAnswerClusterDto>> GetNoAnswersAsync(
        int pageNumber, int pageSize, string? reason = null, string? search = null, Guid? appId = null);

    // ── Reported answers review queue ──────────────────────────────────
    Task<PaginatedList<ReportedAnswerDto>> GetReportedAnswersAsync(
        int pageNumber, int pageSize, bool? resolved = null, string? search = null, Guid? appId = null);

    Task ResolveReportedAnswerAsync(Guid id, bool resolved);
}
