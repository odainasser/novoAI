using System.Net.Http.Json;
using Web.Models.Common;
using Web.Models.Inventory;

namespace Web.Services;

public class ClientStocktakeService : IStocktakeClientService
{
    private readonly HttpClient _httpClient;

    public ClientStocktakeService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PaginatedList<StocktakeDto>> GetAllAsync(
        int pageNumber, int pageSize, string? search = null, string? type = null, string? status = null,
        Guid? warehouseId = null, DateTime? fromDate = null, DateTime? toDate = null, Guid? branchId = null)
    {
        var url = $"api/stocktakes?pageNumber={pageNumber}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search))
            url += $"&search={Uri.EscapeDataString(search)}";
        if (!string.IsNullOrEmpty(type))
            url += $"&type={Uri.EscapeDataString(type)}";
        if (!string.IsNullOrEmpty(status))
            url += $"&status={Uri.EscapeDataString(status)}";
        if (warehouseId.HasValue)
            url += $"&warehouseId={warehouseId.Value}";
        if (fromDate.HasValue)
            url += $"&fromDate={fromDate.Value:yyyy-MM-dd}";
        if (toDate.HasValue)
            url += $"&toDate={toDate.Value:yyyy-MM-dd}";
        if (branchId.HasValue)
            url += $"&branchId={branchId.Value}";

        return await _httpClient.GetFromJsonAsync<PaginatedList<StocktakeDto>>(url)
               ?? new PaginatedList<StocktakeDto>(new List<StocktakeDto>(), 0, pageNumber, pageSize);
    }

    public async Task<StocktakeDto?> GetByIdAsync(Guid id, Guid? branchId = null)
    {
        try
        {
            var url = $"api/stocktakes/{id}";
            if (branchId.HasValue) url += $"?branchId={branchId.Value}";
            return await _httpClient.GetFromJsonAsync<StocktakeDto>(url);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<StocktakeDto> CreateAsync(CreateStocktakeRequest request, Guid? branchId = null)
    {
        var url = "api/stocktakes" + (branchId.HasValue ? $"?branchId={branchId.Value}" : string.Empty);
        var response = await _httpClient.PostAsJsonAsync(url, request);
        await response.HandleErrorAsync();
        return await response.Content.ReadFromJsonAsync<StocktakeDto>()
               ?? throw new Exception("Failed to create stocktake");
    }

    public async Task<StocktakeDto> StartAsync(Guid id, Guid? branchId = null)
        => await PostActionAsync($"api/stocktakes/{id}/start", branchId);

    public async Task<StocktakeDto> SaveCountsAsync(Guid id, SaveStocktakeCountsRequest request, Guid? branchId = null)
    {
        var url = $"api/stocktakes/{id}/counts" + (branchId.HasValue ? $"?branchId={branchId.Value}" : string.Empty);
        var response = await _httpClient.PutAsJsonAsync(url, request);
        await response.HandleErrorAsync();
        return await response.Content.ReadFromJsonAsync<StocktakeDto>()
               ?? throw new Exception("Failed to save counts");
    }

    public async Task<StocktakeDto> CompleteAsync(Guid id, Guid? branchId = null)
        => await PostActionAsync($"api/stocktakes/{id}/complete", branchId);

    public async Task<StocktakeDto> ApproveAsync(Guid id, ApproveStocktakeRequest request, Guid? branchId = null)
    {
        var url = $"api/stocktakes/{id}/approve" + (branchId.HasValue ? $"?branchId={branchId.Value}" : string.Empty);
        var response = await _httpClient.PostAsJsonAsync(url, request);
        await response.HandleErrorAsync();
        return await response.Content.ReadFromJsonAsync<StocktakeDto>()
               ?? throw new Exception("Failed to approve stocktake");
    }

    public async Task<StocktakeDto> CancelAsync(Guid id, Guid? branchId = null)
        => await PostActionAsync($"api/stocktakes/{id}/cancel", branchId);

    private async Task<StocktakeDto> PostActionAsync(string path, Guid? branchId)
    {
        var url = path + (branchId.HasValue ? $"?branchId={branchId.Value}" : string.Empty);
        var response = await _httpClient.PostAsync(url, null);
        await response.HandleErrorAsync();
        return await response.Content.ReadFromJsonAsync<StocktakeDto>()
               ?? throw new Exception("Stocktake action failed");
    }
}
