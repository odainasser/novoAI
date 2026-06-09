using Domain.Entities;
using Domain.Enums;
using Domain.Repositories;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class UserLogRepository : Repository<UserLog>, IUserLogRepository
{
    public UserLogRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<UserLog>> GetLogsByUserIdAsync(Guid userId)
    {
        return await _dbSet
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync();
    }

    public async Task<IEnumerable<UserLog>> GetLatestLogsAsync(int count)
    {
        return await _dbSet
            .OrderByDescending(l => l.Timestamp)
            .Take(count)
            .ToListAsync();
    }

    public async Task<(IEnumerable<UserLog> Items, int TotalCount)> GetPagedLogsAsync(int pageNumber, int pageSize, Guid? userId = null, string? entityName = null, string? entityId = null, string? search = null, string? matchActions = null, string? matchEntities = null)
    {
        var query = _dbSet.AsQueryable();

        if (userId.HasValue)
        {
            query = query.Where(l => l.UserId == userId.Value);
        }

        if (!string.IsNullOrEmpty(entityName))
        {
            query = query.Where(l => l.EntityName == entityName);
        }

        if (!string.IsNullOrEmpty(entityId))
        {
            query = query.Where(l => l.EntityId == entityId);
        }

        if (!string.IsNullOrEmpty(search))
        {
            var actionsList = matchActions?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new();
            var entitiesList = matchEntities?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new();

            var actionFilters = actionsList
                .Select(action => Enum.TryParse<AuditAction>(action, true, out var parsedAction)
                    ? parsedAction
                    : (AuditAction?)null)
                .Where(action => action.HasValue)
                .Select(action => action!.Value)
                .Distinct()
                .ToList();

            query = query.Where(l =>
                l.UserName.Contains(search) ||
                (actionFilters.Count > 0 && actionFilters.Contains(l.Action)) ||
                (entitiesList.Count > 0 && entitiesList.Contains(l.EntityName!)));
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(l => l.Timestamp)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }
}
