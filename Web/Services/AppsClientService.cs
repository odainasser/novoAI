using System.Net.Http.Json;
using System.Text.Json;
using Web.Models.Apps;

namespace Web.Services;

public class AppsClientService : IAppsClientService
{
    private readonly HttpClient _httpClient;

    public AppsClientService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<AppDto>> GetAppsAsync(CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<List<AppDto>>("api/apps", cancellationToken) ?? new();

    public async Task<AppDto> CreateAppAsync(SaveAppRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/apps", request, cancellationToken);
        await ThrowOnError(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<AppDto>(cancellationToken: cancellationToken)
               ?? throw new ApplicationException("Empty response.");
    }

    public async Task UpdateAppAsync(Guid id, SaveAppRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/apps/{id}", request, cancellationToken);
        await ThrowOnError(response, cancellationToken);
    }

    public async Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/apps/{id}/active?isActive={(isActive ? "true" : "false")}", null, cancellationToken);
        await ThrowOnError(response, cancellationToken);
    }

    // Surface the API's { error: "..." } message; fall back to the status code.
    private static async Task ThrowOnError(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;

        string message = $"Request failed ({(int)response.StatusCode}).";
        try
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!string.IsNullOrWhiteSpace(body))
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.ValueKind == JsonValueKind.Object
                    && doc.RootElement.TryGetProperty("error", out var err)
                    && err.ValueKind == JsonValueKind.String)
                    message = err.GetString()!;
            }
        }
        catch { /* keep the generic message */ }

        throw new ApplicationException(message);
    }
}
