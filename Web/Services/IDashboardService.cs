using Web.Models.Dashboard;

namespace Web.Services;

public interface IDashboardClientService
{
    Task<DashboardSummaryDto> GetSummaryAsync();
    Task<List<WarehouseStockDto>> GetWarehouseCurrentStockAsync(DateTime? fromDate = null, DateTime? toDate = null);
}
