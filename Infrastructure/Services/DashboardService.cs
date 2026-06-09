using Application.Features.Dashboard;
using Application.Services;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private readonly ApplicationDbContext _context;

    public DashboardService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync()
    {
        var totalWarehouses = await _context.Warehouses.CountAsync();
        var totalBranches = await _context.Branches.CountAsync();
        var totalTerminals = await _context.Terminals.CountAsync();

        var userStats = await _context.Users
            .GroupBy(u => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Active = g.Count(u => u.IsActive)
            })
            .FirstOrDefaultAsync();

        return new DashboardSummaryDto
        {
            TotalWarehouses = totalWarehouses,
            TotalBranches = totalBranches,
            TotalTerminals = totalTerminals,
            TotalUsers = userStats?.Total ?? 0,
            ActiveUsers = userStats?.Active ?? 0
        };
    }
}
