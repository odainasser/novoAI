using Application.Common.Models;
using Application.Features.UserLogs;

namespace Application.Services;

public interface IUserLogService
{
    Task LogAsync(CreateUserLogRequest request);
    Task<PaginatedList<UserLogDto>> GetLogsAsync(int pageNumber, int pageSize, Guid? userId = null, string? entityName = null, string? entityId = null, string? culture = null, string? search = null, string? matchActions = null, string? matchEntities = null);
}
