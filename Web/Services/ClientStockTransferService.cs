using System.Net.Http.Json;
using Web.Models.Common;
using Web.Models.Inventory;

namespace Web.Services;

public class ClientStockTransferService : IStockTransferClientService
{
    private readonly HttpClient _httpClient;

    public ClientStockTransferService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PaginatedList<StockTransferDto>> GetAllAsync(
        int pageNumber, int pageSize, string? search = null,
        Guid? warehouseId = null, string? transferType = null,
        DateTime? fromDate = null, DateTime? toDate = null,
        Guid? branchId = null)
    {
        var url = $"api/stock-transfers?pageNumber={pageNumber}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search))
            url += $"&search={Uri.EscapeDataString(search)}";
        if (warehouseId.HasValue)
            url += $"&warehouseId={warehouseId.Value}";
        if (!string.IsNullOrEmpty(transferType))
            url += $"&transferType={Uri.EscapeDataString(transferType)}";
        if (fromDate.HasValue)
            url += $"&fromDate={fromDate.Value:yyyy-MM-dd}";
        if (toDate.HasValue)
            url += $"&toDate={toDate.Value:yyyy-MM-dd}";
        if (branchId.HasValue)
            url += $"&branchId={branchId.Value}";

        return await _httpClient.GetFromJsonAsync<PaginatedList<StockTransferDto>>(url)
               ?? new PaginatedList<StockTransferDto>(new List<StockTransferDto>(), 0, pageNumber, pageSize);
    }

    public async Task<StockTransferDto?> GetByIdAsync(Guid id, Guid? branchId = null)
    {
        try
        {
            var url = $"api/stock-transfers/{id}";
            if (branchId.HasValue) url += $"?branchId={branchId.Value}";
            return await _httpClient.GetFromJsonAsync<StockTransferDto>(url);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<StockTransferDto> CreateAsync(CreateStockTransferRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/stock-transfers", request);
        await response.HandleErrorAsync();
        return await response.Content.ReadFromJsonAsync<StockTransferDto>()
               ?? throw new Exception("Failed to create stock transfer");
    }

    public async Task DeleteAsync(Guid id)
    {
        var response = await _httpClient.DeleteAsync($"api/stock-transfers/{id}");
        await response.HandleErrorAsync();
    }
}
