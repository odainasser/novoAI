using Web.Models.Dashboard;

namespace Web.Services;

public interface IDashboardClientService
{
    Task<DashboardSummaryDto> GetSummaryAsync();
    Task<List<WarehouseProductStatsDto>> GetWarehouseProductStatsAsync(DateTime? fromDate = null, DateTime? toDate = null);
    Task<List<WarehouseStockDto>> GetWarehouseCurrentStockAsync(DateTime? fromDate = null, DateTime? toDate = null);
    Task<List<MonthlyRevenueDto>> GetMonthlyRevenueAsync(int months = 12);
}
