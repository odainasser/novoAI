using System.Net.Http.Json;
using Web.Models.Assistant;

namespace Web.Services;

public class ClientAssistantService : IAssistantClientService
{
    private readonly HttpClient _httpClient;

    public ClientAssistantService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<AssistantResponse> AskAsync(AssistantRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/assistant/ask", request, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
            return new AssistantResponse { Answer = "AI assistant is currently disabled." };

        if ((int)response.StatusCode == 429)
            return new AssistantResponse { Answer = "Please wait a moment before sending another message." };

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<AssistantResponse>(cancellationToken: cancellationToken)
               ?? new AssistantResponse { Answer = "No response received." };
    }

    public async Task ReportAsync(AssistantReportRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/assistant/report", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
