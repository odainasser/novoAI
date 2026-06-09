using Application.Features.Dashboard;

namespace Application.Services;

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync();
    Task<List<WarehouseProductStatsDto>> GetWarehouseProductStatsAsync(DateTime? fromDate = null, DateTime? toDate = null);
    Task<List<WarehouseStockDto>> GetWarehouseCurrentStockAsync(DateTime? fromDate = null, DateTime? toDate = null);
    Task<List<MonthlyRevenueDto>> GetMonthlyRevenueAsync(int months = 12);
}
