using System.Net.Http.Json;
using Web.Models.Common;
using Web.Models.UserLogs;

namespace Web.Services;

public class ClientUserLogService : IUserLogService
{
    private readonly HttpClient _httpClient;

    public ClientUserLogService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task LogAsync(CreateUserLogRequest request)
    {
        // Client-side logging not implemented/exposed via API yet.
        await Task.CompletedTask;
    }

    public async Task<PaginatedList<UserLogDto>> GetLogsAsync(int pageNumber, int pageSize, Guid? userId = null, string? entityName = null, string? entityId = null, string? culture = null, string? search = null, string? matchActions = null, string? matchEntities = null)
    {
        var url = $"api/logs?pageNumber={pageNumber}&pageSize={pageSize}";
        if (userId.HasValue)
        {
            url += $"&userId={userId}";
        }
        if (!string.IsNullOrEmpty(entityName))
        {
            url += $"&entityName={Uri.EscapeDataString(entityName)}";
        }
        if (!string.IsNullOrEmpty(entityId))
        {
            url += $"&entityId={Uri.EscapeDataString(entityId)}";
        }
        if (!string.IsNullOrEmpty(culture))
        {
            url += $"&culture={Uri.EscapeDataString(culture)}";
        }
        if (!string.IsNullOrEmpty(search))
        {
            url += $"&search={Uri.EscapeDataString(search)}";
        }
        if (!string.IsNullOrEmpty(matchActions))
        {
            url += $"&matchActions={Uri.EscapeDataString(matchActions)}";
        }
        if (!string.IsNullOrEmpty(matchEntities))
        {
            url += $"&matchEntities={Uri.EscapeDataString(matchEntities)}";
        }
        return await _httpClient.GetFromJsonAsync<PaginatedList<UserLogDto>>(url) 
               ?? new PaginatedList<UserLogDto>(new List<UserLogDto>(), 0, pageNumber, pageSize);
    }
}
