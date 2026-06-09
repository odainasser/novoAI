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
}
