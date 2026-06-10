using System.Net.Http.Json;
using Web.Models.Assistant;
using Web.Models.Common;

namespace Web.Services;

public class ClientAssistantAdminService : IAssistantAdminService
{
    private readonly HttpClient _httpClient;

    public ClientAssistantAdminService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PaginatedList<AssistantInteractionDto>> GetInteractionsAsync(
        int pageNumber, int pageSize, string? search = null,
        bool? unansweredOnly = null, bool? confirmedOnly = null, Guid? appId = null)
    {
        var url = $"api/assistant-admin/interactions?pageNumber={pageNumber}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search)) url += $"&search={Uri.EscapeDataString(search)}";
        if (unansweredOnly.HasValue) url += $"&unansweredOnly={unansweredOnly.Value}";
        if (confirmedOnly.HasValue) url += $"&confirmedOnly={confirmedOnly.Value}";
        if (appId.HasValue) url += $"&appId={appId.Value}";

        return await _httpClient.GetFromJsonAsync<PaginatedList<AssistantInteractionDto>>(url)
               ?? new PaginatedList<AssistantInteractionDto>(new(), 0, pageNumber, pageSize);
    }

    public async Task<List<AppOptionDto>> GetAppOptionsAsync() =>
        await _httpClient.GetFromJsonAsync<List<AppOptionDto>>("api/assistant-admin/app-options") ?? new();

    public async Task<AssistantPlanOptionsDto> GetPlanOptionsAsync() =>
        await _httpClient.GetFromJsonAsync<AssistantPlanOptionsDto>("api/assistant-admin/plan-options")
        ?? new AssistantPlanOptionsDto();

    public async Task ConfirmPlanAsync(Guid id, ConfirmAssistantPlanRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/assistant-admin/interactions/{id}/confirm-plan", request);
        await response.HandleErrorAsync();
    }

    public async Task<AssistantInteractionDto?> GetInteractionAsync(Guid id)
    {
        try { return await _httpClient.GetFromJsonAsync<AssistantInteractionDto>($"api/assistant-admin/interactions/{id}"); }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound) { return null; }
    }

    public async Task<PaginatedList<NoAnswerClusterDto>> GetNoAnswersAsync(
        int pageNumber, int pageSize, string? reason = null, string? search = null, Guid? appId = null)
    {
        var url = $"api/assistant-admin/no-answers?pageNumber={pageNumber}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(reason)) url += $"&reason={Uri.EscapeDataString(reason)}";
        if (!string.IsNullOrWhiteSpace(search)) url += $"&search={Uri.EscapeDataString(search)}";
        if (appId.HasValue) url += $"&appId={appId.Value}";

        return await _httpClient.GetFromJsonAsync<PaginatedList<NoAnswerClusterDto>>(url)
               ?? new PaginatedList<NoAnswerClusterDto>(new(), 0, pageNumber, pageSize);
    }

    public async Task<PaginatedList<ReportedAnswerDto>> GetReportedAnswersAsync(
        int pageNumber, int pageSize, bool? resolved = null, string? search = null, Guid? appId = null)
    {
        var url = $"api/assistant-admin/reported?pageNumber={pageNumber}&pageSize={pageSize}";
        if (resolved.HasValue) url += $"&resolved={resolved.Value}";
        if (!string.IsNullOrWhiteSpace(search)) url += $"&search={Uri.EscapeDataString(search)}";
        if (appId.HasValue) url += $"&appId={appId.Value}";

        return await _httpClient.GetFromJsonAsync<PaginatedList<ReportedAnswerDto>>(url)
               ?? new PaginatedList<ReportedAnswerDto>(new(), 0, pageNumber, pageSize);
    }

    public async Task ResolveReportedAnswerAsync(Guid id, bool resolved)
    {
        var response = await _httpClient.PostAsync($"api/assistant-admin/reported/{id}/resolve?resolved={resolved}", null);
        await response.HandleErrorAsync();
    }
}
