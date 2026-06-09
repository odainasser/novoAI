using Web.Models.Dashboard;

namespace Web.Services;

public interface IDashboardClientService
{
    Task<DashboardSummaryDto> GetSummaryAsync();
}
