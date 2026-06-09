using Domain.Entities;

namespace Domain.Repositories;

public interface IUserLogRepository : IRepository<UserLog>
{
    Task<IEnumerable<UserLog>> GetLogsByUserIdAsync(Guid userId);
    Task<IEnumerable<UserLog>> GetLatestLogsAsync(int count);
    Task<(IEnumerable<UserLog> Items, int TotalCount)> GetPagedLogsAsync(int pageNumber, int pageSize, Guid? userId = null, string? entityName = null, string? entityId = null, string? search = null, string? matchActions = null, string? matchEntities = null);
}
