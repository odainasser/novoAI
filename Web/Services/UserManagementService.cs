using System.Net.Http.Json;
using System.Text.Json;
using Web.Models.Common;
using Web.Models.Users;

namespace Web.Services;

public class UserManagementService : IUserManagementService
{
    private readonly HttpClient _httpClient;

    public UserManagementService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PaginatedList<UserResponse>> GetAllUsersAsync(int pageNumber, int pageSize, string? role = null, string? search = null, bool? isActive = null)
    {
        var url = $"api/users?pageNumber={pageNumber}&pageSize={pageSize}";
        if (!string.IsNullOrEmpty(role))
        {
            url += $"&role={Uri.EscapeDataString(role)}";
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            url += $"&search={Uri.EscapeDataString(search)}";
        }
        if (isActive.HasValue)
        {
            url += $"&isActive={isActive.Value}";
        }
        return await _httpClient.GetFromJsonAsync<PaginatedList<UserResponse>>(url) 
               ?? new PaginatedList<UserResponse>(new List<UserResponse>(), 0, pageNumber, pageSize);
    }

    public async Task<UserResponse?> GetUserByIdAsync(Guid userId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<UserResponse>($"api/users/{userId}");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<UserResponse> CreateUserAsync(CreateUserRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/users", request);
        await response.HandleErrorAsync();
        return await response.Content.ReadFromJsonAsync<UserResponse>() ?? throw new Exception("Failed to create user");
    }

    public async Task<UserResponse> UpdateUserAsync(Guid userId, UpdateUserRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/users/{userId}", request);
        await response.HandleErrorAsync();
        return await response.Content.ReadFromJsonAsync<UserResponse>() ?? throw new Exception("Failed to update user");
    }

    public async Task<UserResponse> UpdateProfileAsync(UpdateUserRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync("api/users/profile", request);
        await response.HandleErrorAsync();
        return await response.Content.ReadFromJsonAsync<UserResponse>() ?? throw new Exception("Failed to update profile");
    }

    public async Task DeleteUserAsync(Guid userId)
    {
        var response = await _httpClient.DeleteAsync($"api/users/{userId}");
        await response.HandleErrorAsync();
    }

    public async Task<UserResponse> AssignRolesToUserAsync(Guid userId, AssignRolesRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/users/{userId}/roles", request);
        await response.HandleErrorAsync();
        return await response.Content.ReadFromJsonAsync<UserResponse>() ?? throw new Exception("Failed to assign roles");
    }

    public async Task<bool> CheckEmailExistsAsync(string email)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<JsonElement>($"api/users/exists?email={Uri.EscapeDataString(email)}");
            return response.GetProperty("exists").GetBoolean();
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<Guid>> GetUserBranchIdsAsync(Guid userId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<Guid>>($"api/users/{userId}/branches")
                   ?? new List<Guid>();
        }
        catch (HttpRequestException ex) when (
            ex.StatusCode == System.Net.HttpStatusCode.NotFound ||
            ex.StatusCode == System.Net.HttpStatusCode.Forbidden ||
            ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            return new List<Guid>();
        }
    }

    public async Task SetUserBranchesAsync(Guid userId, List<Guid> branchIds)
    {
        var response = await _httpClient.PutAsJsonAsync(
            $"api/users/{userId}/branches",
            new { BranchIds = branchIds });
        await response.HandleErrorAsync();
    }
}
