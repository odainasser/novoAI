using Web.Models.Assistant;

namespace Web.Services;

public interface IAssistantClientService
{
    Task<AssistantResponse> AskAsync(AssistantRequest request, CancellationToken cancellationToken = default);

    /// <summary>Report an answer (snapshot + optional feedback) for admin review.</summary>
    Task ReportAsync(AssistantReportRequest request, CancellationToken cancellationToken = default);
}
