using System.Net.Http.Json;
using System.Text.Json;
using Web.Models.Common;
using Web.Models.Warehouses;

namespace Web.Services;

public class ClientWarehouseService : IWarehouseService
{
    private readonly HttpClient _httpClient;

    public ClientWarehouseService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PaginatedList<WarehouseDto>> GetAllWarehousesAsync(
        int pageNumber, int pageSize, string? search = null, bool? isActive = null, Guid? warehouseTypeId = null, Guid? branchId = null)
    {
        var url = $"api/warehouses?pageNumber={pageNumber}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search))
            url += $"&search={Uri.EscapeDataString(search)}";
        if (isActive.HasValue)
            url += $"&isActive={isActive.Value}";
        if (warehouseTypeId.HasValue)
            url += $"&warehouseTypeId={warehouseTypeId.Value}";
        if (branchId.HasValue)
            url += $"&branchId={branchId.Value}";

        return await _httpClient.GetFromJsonAsync<PaginatedList<WarehouseDto>>(url)
               ?? new PaginatedList<WarehouseDto>(new List<WarehouseDto>(), 0, pageNumber, pageSize);
    }

    public async Task<List<WarehouseDto>> GetActiveWarehousesAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<WarehouseDto>>("api/warehouses/active")
               ?? new List<WarehouseDto>();
    }

    public async Task<WarehouseDto?> GetWarehouseByIdAsync(Guid id)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<WarehouseDto>($"api/warehouses/{id}");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<WarehouseDto> CreateWarehouseAsync(CreateWarehouseRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/warehouses", request);
        await response.HandleErrorAsync();
        return await response.Content.ReadFromJsonAsync<WarehouseDto>() ?? throw new Exception("Failed to create warehouse");
    }

    public async Task<WarehouseDto> UpdateWarehouseAsync(Guid id, UpdateWarehouseRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/warehouses/{id}", request);
        await response.HandleErrorAsync();
        return await response.Content.ReadFromJsonAsync<WarehouseDto>() ?? throw new Exception("Failed to update warehouse");
    }

    public async Task DeleteWarehouseAsync(Guid id)
    {
        var response = await _httpClient.DeleteAsync($"api/warehouses/{id}");
        await response.HandleErrorAsync();
    }

    public async Task<bool> CheckCentralWarehouseExistsAsync(Guid? excludeWarehouseId = null)
    {
        try
        {
            var url = "api/warehouses/central-exists";
            if (excludeWarehouseId.HasValue)
                url += $"?excludeWarehouseId={excludeWarehouseId.Value}";

            var response = await _httpClient.GetFromJsonAsync<JsonElement>(url);
            return response.GetProperty("exists").GetBoolean();
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> CheckWarehouseExistsAsync(string nameEn, string nameAr, Guid? excludeWarehouseId = null)
    {
        try
        {
            var url = $"api/warehouses/exists?nameEn={Uri.EscapeDataString(nameEn)}&nameAr={Uri.EscapeDataString(nameAr)}";
            if (excludeWarehouseId.HasValue)
                url += $"&excludeWarehouseId={excludeWarehouseId.Value}";

            var response = await _httpClient.GetFromJsonAsync<JsonElement>(url);
            return response.GetProperty("exists").GetBoolean();
        }
        catch
        {
            return false;
        }
    }
}
