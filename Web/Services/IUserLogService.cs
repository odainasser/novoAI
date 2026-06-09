using Web.Models.Common;
using Web.Models.UserLogs;

namespace Web.Services;

public interface IUserLogService
{
    Task LogAsync(CreateUserLogRequest request);
    Task<PaginatedList<UserLogDto>> GetLogsAsync(int pageNumber, int pageSize, Guid? userId = null, string? entityName = null, string? entityId = null, string? culture = null, string? search = null, string? matchActions = null, string? matchEntities = null);
}
