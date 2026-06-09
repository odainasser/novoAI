using Application.Common.Models;
using Application.Features.Assistant;

namespace Application.Services;

/// <summary>
/// Admin surface for the tool-calling assistant. The single "plan" page shows turns
/// (question → answer, tools called → module/domain + entities); a reviewer can
/// correct a turn's plan and save it as a DRAFT governed plan, then manage the plan
/// library (promote → confirmed, deprecate, edit the executable definition). Confirmed
/// plans are what the engine matches and executes. Nothing is auto-applied.
/// </summary>
public interface IAssistantAdminService
{
    // ── Interactions (the turn log) ────────────────────────────────────
    Task<PaginatedList<AssistantInteractionDto>> GetInteractionsAsync(
        int pageNumber, int pageSize, string? search = null,
        bool? unansweredOnly = null, bool? confirmedOnly = null);

    /// <summary>The tool/domain/entity vocabulary for the correction dropdowns
    /// (fetched from the ByteMart-owned tool catalog).</summary>
    Task<AssistantPlanOptionsDto> GetPlanOptionsAsync();

    /// <summary>
    /// Save a reviewer's corrected plan for a turn as a CONFIRMED governed plan
    /// (one per match key). The engine uses it on the next matching question.
    /// </summary>
    Task ConfirmPlanAsync(Guid interactionId, ConfirmAssistantPlanRequest request);

    /// <summary>Load one interaction (to seed the plan editor from a no-answer cluster).</summary>
    Task<AssistantInteractionDto?> GetInteractionAsync(Guid id);

    // ── No-answer review queue ─────────────────────────────────────────
    /// <summary>
    /// The clustered no-answer queue: ranked with mis-refusals (backward bugs) above
    /// coverage gaps, then by frequency. EmptyResult is excluded unless explicitly filtered.
    /// </summary>
    Task<PaginatedList<NoAnswerClusterDto>> GetNoAnswersAsync(
        int pageNumber, int pageSize, string? reason = null, string? search = null);

    // ── Reported answers review queue ──────────────────────────────────
    /// <summary>User-reported answers (snapshots), newest first; filter by resolved state.</summary>
    Task<PaginatedList<ReportedAnswerDto>> GetReportedAnswersAsync(
        int pageNumber, int pageSize, bool? resolved = null, string? search = null);

    /// <summary>Mark a reported answer resolved (or re-open it).</summary>
    Task ResolveReportedAnswerAsync(Guid id, bool resolved);
}
