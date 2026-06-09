using System.Net.Http.Json;
using System.Text.Json;
using Web.Models.Common;
using Web.Models.Roles;

namespace Web.Services;

public class RoleManagementService : IRoleManagementService
{
    private readonly HttpClient _httpClient;

    public RoleManagementService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PaginatedList<RoleResponse>> GetAllRolesAsync(int pageNumber, int pageSize)
    {
        return await _httpClient.GetFromJsonAsync<PaginatedList<RoleResponse>>($"api/roles?pageNumber={pageNumber}&pageSize={pageSize}") 
               ?? new PaginatedList<RoleResponse>(new List<RoleResponse>(), 0, pageNumber, pageSize);
    }

    public async Task<RoleDetailResponse?> GetRoleByIdAsync(Guid roleId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<RoleDetailResponse>($"api/roles/{roleId}");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<RoleResponse> CreateRoleAsync(CreateRoleRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/roles", request);
        await response.HandleErrorAsync();
        return await response.Content.ReadFromJsonAsync<RoleResponse>() ?? throw new Exception("Failed to create role");
    }

    public async Task<RoleResponse> UpdateRoleAsync(Guid roleId, UpdateRoleRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/roles/{roleId}", request);
        await response.HandleErrorAsync();
        return await response.Content.ReadFromJsonAsync<RoleResponse>() ?? throw new Exception("Failed to update role");
    }

    public async Task DeleteRoleAsync(Guid roleId)
    {
        var response = await _httpClient.DeleteAsync($"api/roles/{roleId}");
        await response.HandleErrorAsync();
    }

    public async Task<RoleDetailResponse> AssignPermissionsToRoleAsync(Guid roleId, AssignPermissionsRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/roles/{roleId}/permissions", request);
        await response.HandleErrorAsync();
        return await response.Content.ReadFromJsonAsync<RoleDetailResponse>() ?? throw new Exception("Failed to assign permissions");
    }

    public async Task<bool> CheckRoleNameExistsAsync(string name, Guid? excludeRoleId = null)
    {
        try
        {
            var url = $"api/roles/exists?name={Uri.EscapeDataString(name)}";
            if (excludeRoleId.HasValue)
            {
                url += $"&excludeRoleId={excludeRoleId.Value}";
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
