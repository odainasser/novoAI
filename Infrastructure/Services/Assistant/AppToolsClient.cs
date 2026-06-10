using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Infrastructure.Services.Assistant;

/// <summary>
/// HTTP client for a registered app's /api/assistant-data tool-provider surface.
/// Every call forwards the CURRENT USER'S own bearer token (the apps share/accept
/// the same JWT signing configuration), so the app enforces permissions and any
/// branch lock against the real caller — ByteAI never holds elevated credentials.
/// The target app is chosen per call via its registered base URL.
/// </summary>
internal sealed class AppToolsClient
{
    private static readonly JsonSerializerOptions Camel = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AppToolsClient(IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor)
    {
        _httpClientFactory = httpClientFactory;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<IReadOnlyList<AppToolDescriptor>> GetCatalogAsync(string baseUrl, CancellationToken ct)
    {
        using var response = await CreateClient().GetAsync(Url(baseUrl, "api/assistant-data/tools"), ct);
        response.EnsureSuccessStatusCode();
        var tools = await response.Content.ReadFromJsonAsync<List<AppToolDescriptor>>(Camel, ct);
        return tools ?? new List<AppToolDescriptor>();
    }

    public async Task<AppToolExecuteResult> ExecuteAsync(
        string baseUrl, string name, JsonElement arguments, Guid? branchId, string locale, CancellationToken ct)
    {
        var request = new { name, arguments, branchId, locale };
        using var response = await CreateClient()
            .PostAsJsonAsync(Url(baseUrl, "api/assistant-data/execute"), request, Camel, ct);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AppToolExecuteResult>(Camel, ct);
        return result ?? new AppToolExecuteResult();
    }

    public async Task<IReadOnlyList<Guid>> GetBranchWarehouseIdsAsync(string baseUrl, Guid branchId, CancellationToken ct)
    {
        using var response = await CreateClient()
            .GetAsync(Url(baseUrl, $"api/assistant-data/branch-context/{branchId}"), ct);
        response.EnsureSuccessStatusCode();
        var context = await response.Content.ReadFromJsonAsync<AppBranchContext>(Camel, ct);
        return context?.WarehouseIds ?? new List<Guid>();
    }

    private static Uri Url(string baseUrl, string path) =>
        new(new Uri(baseUrl.TrimEnd('/') + "/"), path);

    // A fresh client per call (factory-managed handlers) carrying the caller's token.
    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient("AppTools");

        var authorization = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(authorization)
            && authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = authorization["Bearer ".Length..].Trim();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }
}

internal sealed class AppToolDescriptor
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public List<string> Entities { get; set; } = new();
    public List<string> Permissions { get; set; } = new();
    public bool CrossBranch { get; set; }
    public bool IsMixing { get; set; }
    public JsonElement ParametersSchema { get; set; }
}

internal sealed class AppToolExecuteResult
{
    public string Status { get; set; } = "error";
    public JsonElement? Data { get; set; }
}

internal sealed class AppBranchContext
{
    public List<Guid> WarehouseIds { get; set; } = new();
}
