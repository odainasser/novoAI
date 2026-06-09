using System.Net.Http.Json;
using System.Text.Json;
using Web.Models.Common;
using Web.Models.Terminals;

namespace Web.Services;

public class ClientTerminalService : ITerminalService
{
    private readonly HttpClient _httpClient;

    public ClientTerminalService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PaginatedList<TerminalDto>> GetAllTerminalsAsync(
        int pageNumber, int pageSize, string? search = null, bool? isActive = null, Guid? branchId = null)
    {
        var url = $"api/terminals?pageNumber={pageNumber}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search))
            url += $"&search={Uri.EscapeDataString(search)}";
        if (isActive.HasValue)
            url += $"&isActive={isActive.Value}";
        if (branchId.HasValue)
            url += $"&branchId={branchId.Value}";

        return await _httpClient.GetFromJsonAsync<PaginatedList<TerminalDto>>(url)
               ?? new PaginatedList<TerminalDto>(new List<TerminalDto>(), 0, pageNumber, pageSize);
    }

    public async Task<List<TerminalDto>> GetActiveTerminalsAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<TerminalDto>>("api/terminals/active")
               ?? new List<TerminalDto>();
    }

    public async Task<TerminalDto?> GetTerminalByIdAsync(Guid id)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<TerminalDto>($"api/terminals/{id}");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<TerminalDto> CreateTerminalAsync(CreateTerminalRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/terminals", request);
        await response.HandleErrorAsync();
        return await response.Content.ReadFromJsonAsync<TerminalDto>() ?? throw new Exception("Failed to create terminal");
    }

    public async Task<TerminalDto> UpdateTerminalAsync(Guid id, UpdateTerminalRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/terminals/{id}", request);
        await response.HandleErrorAsync();
        return await response.Content.ReadFromJsonAsync<TerminalDto>() ?? throw new Exception("Failed to update terminal");
    }

    public async Task DeleteTerminalAsync(Guid id)
    {
        var response = await _httpClient.DeleteAsync($"api/terminals/{id}");
        await response.HandleErrorAsync();
    }

    public async Task<bool> CheckTerminalExistsAsync(string nameEn, string nameAr, Guid? excludeTerminalId = null)
    {
        try
        {
            var url = $"api/terminals/exists?nameEn={Uri.EscapeDataString(nameEn)}&nameAr={Uri.EscapeDataString(nameAr)}";
            if (excludeTerminalId.HasValue)
                url += $"&excludeTerminalId={excludeTerminalId.Value}";

            var response = await _httpClient.GetFromJsonAsync<JsonElement>(url);
            return response.GetProperty("exists").GetBoolean();
        }
        catch
        {
            return false;
        }
    }
}
