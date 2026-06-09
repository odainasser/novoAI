using System.Net.Http.Json;
using Web.Models.Common;
using Web.Models.Inventory;

namespace Web.Services;

public class ClientStockAdjustmentService : IStockAdjustmentClientService
{
    private readonly HttpClient _httpClient;

    public ClientStockAdjustmentService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PaginatedList<StockAdjustmentDto>> GetAllAsync(
        int pageNumber, int pageSize, string? search = null, string? status = null,
        string? adjustmentType = null, Guid? warehouseId = null, DateTime? fromDate = null, DateTime? toDate = null,
        Guid? branchId = null)
    {
        var url = $"api/stock-adjustments?pageNumber={pageNumber}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search))
            url += $"&search={Uri.EscapeDataString(search)}";
        if (!string.IsNullOrEmpty(status))
            url += $"&status={Uri.EscapeDataString(status)}";
        if (!string.IsNullOrEmpty(adjustmentType))
            url += $"&adjustmentType={Uri.EscapeDataString(adjustmentType)}";
        if (warehouseId.HasValue)
            url += $"&warehouseId={warehouseId.Value}";
        if (fromDate.HasValue)
            url += $"&fromDate={fromDate.Value:yyyy-MM-dd}";
        if (toDate.HasValue)
            url += $"&toDate={toDate.Value:yyyy-MM-dd}";
        if (branchId.HasValue)
            url += $"&branchId={branchId.Value}";

        return await _httpClient.GetFromJsonAsync<PaginatedList<StockAdjustmentDto>>(url)
               ?? new PaginatedList<StockAdjustmentDto>(new List<StockAdjustmentDto>(), 0, pageNumber, pageSize);
    }

    public async Task<StockAdjustmentDto?> GetByIdAsync(Guid id, Guid? branchId = null)
    {
        try
        {
            var url = $"api/stock-adjustments/{id}";
            if (branchId.HasValue) url += $"?branchId={branchId.Value}";
            return await _httpClient.GetFromJsonAsync<StockAdjustmentDto>(url);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<StockAdjustmentDto> CreateAsync(CreateStockAdjustmentRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/stock-adjustments", request);
        await response.HandleErrorAsync();
        return await response.Content.ReadFromJsonAsync<StockAdjustmentDto>()
               ?? throw new Exception("Failed to create stock adjustment");
    }

    public async Task DeleteAsync(Guid id)
    {
        var response = await _httpClient.DeleteAsync($"api/stock-adjustments/{id}");
        await response.HandleErrorAsync();
    }
}
