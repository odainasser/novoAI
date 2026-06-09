using System.Net.Http.Json;
using System.Text.Json;
using Web.Models.Common;
using Web.Models.Branches;

namespace Web.Services;

public class ClientBranchService : IBranchService
{
    private readonly HttpClient _httpClient;

    public ClientBranchService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PaginatedList<BranchDto>> GetAllBranchesAsync(int pageNumber, int pageSize, string? search = null, bool? isActive = null)
    {
        var url = $"api/branches?pageNumber={pageNumber}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search))
        {
            url += $"&search={Uri.EscapeDataString(search)}";
        }
        if (isActive.HasValue)
        {
            url += $"&isActive={isActive.Value}";
        }
        return await _httpClient.GetFromJsonAsync<PaginatedList<BranchDto>>(url)
               ?? new PaginatedList<BranchDto>(new List<BranchDto>(), 0, pageNumber, pageSize);
    }

    public async Task<List<BranchDto>> GetActiveBranchesAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<BranchDto>>("api/branches/active")
               ?? new List<BranchDto>();
    }

    // Branches assigned to the currently authenticated user via UserBranch.
    // Drives the Branch Panel's branch selector. Returns an empty list rather
    // than throwing on 401/403 so the UI can show a "no branches" state.
    public async Task<List<BranchDto>> GetMyBranchesAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<BranchDto>>("api/branches/mine")
                   ?? new List<BranchDto>();
        }
        catch (HttpRequestException ex) when (
            ex.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
            ex.StatusCode == System.Net.HttpStatusCode.Forbidden ||
            ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return new List<BranchDto>();
        }
    }

    public async Task<BranchWarehouseDto?> GetBranchWarehouseAsync(Guid branchId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<BranchWarehouseDto>($"api/branches/{branchId}/warehouse");
        }
        catch (HttpRequestException ex) when (
            ex.StatusCode == System.Net.HttpStatusCode.NotFound ||
            ex.StatusCode == System.Net.HttpStatusCode.Forbidden ||
            ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            return null;
        }
    }

    public async Task<BranchDto?> GetBranchByIdAsync(Guid id)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<BranchDto>($"api/branches/{id}");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<BranchDto> CreateBranchAsync(CreateBranchRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/branches", request);
        await response.HandleErrorAsync();
        return await response.Content.ReadFromJsonAsync<BranchDto>() ?? throw new Exception("Failed to create branch");
    }

    public async Task<BranchDto> UpdateBranchAsync(Guid id, UpdateBranchRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/branches/{id}", request);
        await response.HandleErrorAsync();
        return await response.Content.ReadFromJsonAsync<BranchDto>() ?? throw new Exception("Failed to update branch");
    }

    public async Task DeleteBranchAsync(Guid id)
    {
        var response = await _httpClient.DeleteAsync($"api/branches/{id}");
        await response.HandleErrorAsync();
    }

    public async Task<bool> CheckBranchExistsAsync(string nameEn, string nameAr, Guid? excludeBranchId = null)
    {
        try
        {
            var url = $"api/branches/exists?nameEn={Uri.EscapeDataString(nameEn)}&nameAr={Uri.EscapeDataString(nameAr)}";
            if (excludeBranchId.HasValue)
            {
                url += $"&excludeBranchId={excludeBranchId.Value}";
            }
            var response = await _httpClient.GetFromJsonAsync<JsonElement>(url);
            return response.GetProperty("exists").GetBoolean();
        }
        catch
        {
            return false;
        }
    }
}
