using System.Net.Http.Json;
using Web.Models.Dashboard;

namespace Web.Services;

public class ClientDashboardService : IDashboardClientService
{
    private readonly HttpClient _httpClient;

    public ClientDashboardService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync()
    {
        return await _httpClient.GetFromJsonAsync<DashboardSummaryDto>("api/dashboard/summary")
               ?? new DashboardSummaryDto();
    }

    public async Task<List<WarehouseProductStatsDto>> GetWarehouseProductStatsAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = "api/dashboard/warehouse-stats";
        var queryParams = new List<string>();
        if (fromDate.HasValue)
            queryParams.Add($"fromDate={fromDate.Value:yyyy-MM-dd}");
        if (toDate.HasValue)
            queryParams.Add($"toDate={toDate.Value:yyyy-MM-dd}");
        if (queryParams.Any())
            query += "?" + string.Join("&", queryParams);

        return await _httpClient.GetFromJsonAsync<List<WarehouseProductStatsDto>>(query)
               ?? new List<WarehouseProductStatsDto>();
    }

    public async Task<List<WarehouseStockDto>> GetWarehouseCurrentStockAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = "api/dashboard/warehouse-stock";
        var queryParams = new List<string>();
        if (fromDate.HasValue)
            queryParams.Add($"fromDate={fromDate.Value:yyyy-MM-dd}");
        if (toDate.HasValue)
            queryParams.Add($"toDate={toDate.Value:yyyy-MM-dd}");
        if (queryParams.Any())
            query += "?" + string.Join("&", queryParams);

        return await _httpClient.GetFromJsonAsync<List<WarehouseStockDto>>(query)
               ?? new List<WarehouseStockDto>();
    }

    public async Task<List<MonthlyRevenueDto>> GetMonthlyRevenueAsync(int months = 12)
    {
        return await _httpClient.GetFromJsonAsync<List<MonthlyRevenueDto>>($"api/dashboard/monthly-revenue?months={months}")
               ?? new List<MonthlyRevenueDto>();
    }
}
