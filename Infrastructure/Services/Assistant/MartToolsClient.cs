using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Infrastructure.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services.Assistant;

/// <summary>
/// HTTP client for ByteMart's /api/assistant-data tool-provider surface. Every
/// call forwards the CURRENT USER'S own bearer token (both systems share the JWT
/// signing configuration), so ByteMart enforces permissions and the branch lock
/// against the real caller — this service never holds elevated credentials.
/// </summary>
internal sealed class MartToolsClient
{
    private static readonly JsonSerializerOptions Camel = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public MartToolsClient(IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor)
    {
        _httpClientFactory = httpClientFactory;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<IReadOnlyList<MartToolDescriptor>> GetCatalogAsync(CancellationToken ct)
    {
        using var response = await CreateClient().GetAsync("api/assistant-data/tools", ct);
        response.EnsureSuccessStatusCode();
        var tools = await response.Content.ReadFromJsonAsync<List<MartToolDescriptor>>(Camel, ct);
        return tools ?? new List<MartToolDescriptor>();
    }

    public async Task<MartToolExecuteResult> ExecuteAsync(
        string name, JsonElement arguments, Guid? branchId, string locale, CancellationToken ct)
    {
        var request = new { name, arguments, branchId, locale };
        using var response = await CreateClient().PostAsJsonAsync("api/assistant-data/execute", request, Camel, ct);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<MartToolExecuteResult>(Camel, ct);
        return result ?? new MartToolExecuteResult();
    }

    public async Task<IReadOnlyList<Guid>> GetBranchWarehouseIdsAsync(Guid branchId, CancellationToken ct)
    {
        using var response = await CreateClient().GetAsync($"api/assistant-data/branch-context/{branchId}", ct);
        response.EnsureSuccessStatusCode();
        var context = await response.Content.ReadFromJsonAsync<MartBranchContext>(Camel, ct);
        return context?.WarehouseIds ?? new List<Guid>();
    }

    // A fresh client per call (factory-managed handlers) carrying the caller's token.
    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient("Mart");

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

internal sealed class MartToolDescriptor
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

internal sealed class MartToolExecuteResult
{
    public string Status { get; set; } = "error";
    public JsonElement? Data { get; set; }
}

internal sealed class MartBranchContext
{
    public List<Guid> WarehouseIds { get; set; } = new();
}
