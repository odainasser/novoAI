using System.Net.Http.Json;
using Web.Models.Common;
using Web.Models.Inventory;

namespace Web.Services;

public class ClientInventoryHistoryService : IInventoryHistoryClientService
{
    private readonly HttpClient _httpClient;

    public ClientInventoryHistoryService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PaginatedList<InventoryHistoryDto>> GetAllAsync(
        int pageNumber, int pageSize, Guid? warehouseId = null, Guid? unitId = null,
        string? actionType = null, DateTime? fromDate = null, DateTime? toDate = null, string? referenceType = null, Guid? branchId = null)
    {
        var url = $"api/inventory-history?pageNumber={pageNumber}&pageSize={pageSize}";
        if (warehouseId.HasValue)
            url += $"&warehouseId={warehouseId.Value}";
        if (unitId.HasValue)
            url += $"&unitId={unitId.Value}";
        if (!string.IsNullOrEmpty(actionType))
            url += $"&actionType={Uri.EscapeDataString(actionType)}";
        if (fromDate.HasValue)
            url += $"&fromDate={fromDate.Value:yyyy-MM-dd}";
        if (toDate.HasValue)
            url += $"&toDate={toDate.Value:yyyy-MM-dd}";
        if (!string.IsNullOrEmpty(referenceType))
            url += $"&referenceType={Uri.EscapeDataString(referenceType)}";
        if (branchId.HasValue)
            url += $"&branchId={branchId.Value}";

        return await _httpClient.GetFromJsonAsync<PaginatedList<InventoryHistoryDto>>(url)
               ?? new PaginatedList<InventoryHistoryDto>(new List<InventoryHistoryDto>(), 0, pageNumber, pageSize);
    }

    public async Task<InventoryHistoryDto?> GetByIdAsync(Guid id)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<InventoryHistoryDto>($"api/inventory-history/{id}");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<List<StockBalanceDto>> GetStockBalancesAsync(Guid warehouseId, string? search = null)
    {
        var url = $"api/inventory-history/balances/{warehouseId}";
        if (!string.IsNullOrWhiteSpace(search))
            url += $"?search={Uri.EscapeDataString(search)}";

        return await _httpClient.GetFromJsonAsync<List<StockBalanceDto>>(url)
               ?? new List<StockBalanceDto>();
    }

    public async Task<PaginatedList<StockBalanceDto>> GetAllStockBalancesAsync(
        int pageNumber, int pageSize, string? search = null, Guid? warehouseId = null, string? stockStatus = null,
        Guid? branchId = null)
    {
        var url = $"api/inventory-history/balances?pageNumber={pageNumber}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search))
            url += $"&search={Uri.EscapeDataString(search)}";
        if (warehouseId.HasValue)
            url += $"&warehouseId={warehouseId.Value}";
        if (!string.IsNullOrWhiteSpace(stockStatus))
            url += $"&stockStatus={Uri.EscapeDataString(stockStatus)}";
        if (branchId.HasValue)
            url += $"&branchId={branchId.Value}";

        return await _httpClient.GetFromJsonAsync<PaginatedList<StockBalanceDto>>(url)
               ?? new PaginatedList<StockBalanceDto>(new List<StockBalanceDto>(), 0, pageNumber, pageSize);
    }

    public async Task<int> GetTotalAvailableBySearchAsync(string search)
    {
        var url = $"api/inventory-history/balances/total?search={Uri.EscapeDataString(search)}";
        var result = await _httpClient.GetFromJsonAsync<TotalAvailableResponse>(url);
        return result?.TotalAvailable ?? 0;
    }

    private class TotalAvailableResponse
    {
        public int TotalAvailable { get; set; }
    }
}
