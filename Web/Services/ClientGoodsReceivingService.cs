using System.Net.Http.Json;
using Web.Models.Common;
using Web.Models.Inventory;

namespace Web.Services;

public class ClientGoodsReceivingService : IGoodsReceivingClientService
{
    private readonly HttpClient _httpClient;

    public ClientGoodsReceivingService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PaginatedList<GoodsReceivingNoteDto>> GetAllAsync(
        int pageNumber, int pageSize, string? search = null,
        Guid? supplierId = null, Guid? warehouseId = null, DateTime? fromDate = null, DateTime? toDate = null,
        Guid? branchId = null)
    {
        var url = $"api/goods-receiving?pageNumber={pageNumber}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search))
            url += $"&search={Uri.EscapeDataString(search)}";
        if (supplierId.HasValue)
            url += $"&supplierId={supplierId.Value}";
        if (warehouseId.HasValue)
            url += $"&warehouseId={warehouseId.Value}";
        if (fromDate.HasValue)
            url += $"&fromDate={fromDate.Value:yyyy-MM-dd}";
        if (toDate.HasValue)
            url += $"&toDate={toDate.Value:yyyy-MM-dd}";
        if (branchId.HasValue)
            url += $"&branchId={branchId.Value}";

        return await _httpClient.GetFromJsonAsync<PaginatedList<GoodsReceivingNoteDto>>(url)
               ?? new PaginatedList<GoodsReceivingNoteDto>(new List<GoodsReceivingNoteDto>(), 0, pageNumber, pageSize);
    }

    public async Task<GoodsReceivingNoteDto?> GetByIdAsync(Guid id, Guid? branchId = null)
    {
        try
        {
            var url = $"api/goods-receiving/{id}";
            if (branchId.HasValue) url += $"?branchId={branchId.Value}";
            return await _httpClient.GetFromJsonAsync<GoodsReceivingNoteDto>(url);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<GoodsReceivingNoteDto> CreateAsync(CreateGoodsReceivingNoteRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/goods-receiving", request);
        await response.HandleErrorAsync();
        return await response.Content.ReadFromJsonAsync<GoodsReceivingNoteDto>()
               ?? throw new Exception("Failed to create goods receiving note");
    }

    public async Task DeleteAsync(Guid id)
    {
        var response = await _httpClient.DeleteAsync($"api/goods-receiving/{id}");
        await response.HandleErrorAsync();
    }
}
