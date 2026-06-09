using System.Net.Http.Json;
using Web.Models.Roles;

namespace Web.Services;

public class PermissionManagementService : IPermissionManagementService
{
    private readonly HttpClient _httpClient;

    public PermissionManagementService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<PermissionDto>> GetAllPermissionsAsync()
    {
        return await _httpClient.GetFromJsonAsync<IEnumerable<PermissionDto>>("api/permissions") 
               ?? Enumerable.Empty<PermissionDto>();
    }

    public async Task<IEnumerable<PermissionDto>> GetPermissionsByRoleIdAsync(Guid roleId)
    {
        return await _httpClient.GetFromJsonAsync<IEnumerable<PermissionDto>>($"api/permissions/role/{roleId}") 
               ?? Enumerable.Empty<PermissionDto>();
    }
}
