using System.Net.Http.Json;
using System.Text.Json;
using Web.Models.Common;
using Web.Models.Lookups;

namespace Web.Services;

public class ClientLookupService : ILookupService
{
    private readonly HttpClient _httpClient;

    public ClientLookupService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PaginatedList<LookupDto>> GetAllLookupsAsync(int pageNumber, int pageSize, string? parentCode = null, string? search = null, bool? isActive = null)
    {
        var url = $"api/lookups?pageNumber={pageNumber}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(parentCode))
        {
            url += $"&parentCode={Uri.EscapeDataString(parentCode)}";
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            url += $"&search={Uri.EscapeDataString(search)}";
        }
        if (isActive.HasValue)
        {
            url += $"&isActive={isActive.Value}";
        }
        return await _httpClient.GetFromJsonAsync<PaginatedList<LookupDto>>(url)
               ?? new PaginatedList<LookupDto>(new List<LookupDto>(), 0, pageNumber, pageSize);
    }

    public async Task<List<LookupDto>> GetLookupsByParentAsync(string parentCode)
    {
        return await _httpClient.GetFromJsonAsync<List<LookupDto>>($"api/lookups/byparent?parentCode={Uri.EscapeDataString(parentCode)}")
               ?? new List<LookupDto>();
    }

    public async Task<List<LookupDto>> GetRootLookupsAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<LookupDto>>("api/lookups/roots")
               ?? new List<LookupDto>();
    }

    public async Task<LookupDto?> GetLookupByIdAsync(Guid id)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<LookupDto>($"api/lookups/{id}");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<LookupDto> CreateLookupAsync(CreateLookupRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/lookups", request);
        await response.HandleErrorAsync();
        return await response.Content.ReadFromJsonAsync<LookupDto>() ?? throw new Exception("Failed to create lookup");
    }

    public async Task<LookupDto> UpdateLookupAsync(Guid id, UpdateLookupRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/lookups/{id}", request);
        await response.HandleErrorAsync();
        return await response.Content.ReadFromJsonAsync<LookupDto>() ?? throw new Exception("Failed to update lookup");
    }

    public async Task DeleteLookupAsync(Guid id)
    {
        var response = await _httpClient.DeleteAsync($"api/lookups/{id}");
        await response.HandleErrorAsync();
    }

    public async Task<(bool CodeExists, bool NameEnExists, bool NameArExists)> CheckLookupExistsAsync(string code, string nameEn, string nameAr, Guid? excludeLookupId = null)
    {
        try
        {
            var url = $"api/lookups/exists?code={Uri.EscapeDataString(code)}&nameEn={Uri.EscapeDataString(nameEn)}&nameAr={Uri.EscapeDataString(nameAr)}";
            if (excludeLookupId.HasValue)
            {
                url += $"&excludeLookupId={excludeLookupId.Value}";
            }
            var response = await _httpClient.GetFromJsonAsync<JsonElement>(url);
            return (
                response.GetProperty("codeExists").GetBoolean(),
                response.GetProperty("nameEnExists").GetBoolean(),
                response.GetProperty("nameArExists").GetBoolean()
            );
        }
        catch
        {
            return (false, false, false);
        }
    }
}
