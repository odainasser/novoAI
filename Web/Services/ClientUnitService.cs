using System.Net.Http.Json;
using Web.Models.Common;
using Web.Models.Enums;
using Web.Models.Units;

namespace Web.Services;

public class ClientUnitService : IUnitService
{
    private readonly HttpClient _httpClient;

    public ClientUnitService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PaginatedList<UnitDto>> GetAllAsync(int pageNumber, int pageSize, string? search = null, Guid? productId = null, Guid? unitOfMeasureId = null, bool? isActive = null, Guid? unitTypeId = null, Guid? categoryId = null, Guid? supplierId = null, ItemStatus? status = null)
    {
        var url = $"api/units?pageNumber={pageNumber}&pageSize={pageSize}";

        if (!string.IsNullOrWhiteSpace(search))
            url += $"&search={Uri.EscapeDataString(search)}";
        if (productId.HasValue)
            url += $"&productId={productId.Value}";
        if (unitOfMeasureId.HasValue)
            url += $"&unitOfMeasureId={unitOfMeasureId.Value}";
        if (status.HasValue)
            url += $"&status={(int)status.Value}";
        else if (isActive.HasValue)
            url += $"&isActive={isActive.Value}";
        if (unitTypeId.HasValue)
            url += $"&unitTypeId={unitTypeId.Value}";
        if (categoryId.HasValue)
            url += $"&categoryId={categoryId.Value}";
        if (supplierId.HasValue)
            url += $"&supplierId={supplierId.Value}";

        return await _httpClient.GetFromJsonAsync<PaginatedList<UnitDto>>(url)
               ?? new PaginatedList<UnitDto>(new List<UnitDto>(), 0, pageNumber, pageSize);
    }

    public async Task<UnitDto?> GetByIdAsync(Guid id)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<UnitDto>($"api/units/{id}");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<UnitDto> CreateAsync(CreateUnitRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/units", request);
        await response.HandleErrorAsync();
        return await response.Content.ReadFromJsonAsync<UnitDto>() ?? throw new Exception("Failed to create unit");
    }

    public async Task<UnitDto> UpdateAsync(Guid id, UpdateUnitRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/units/{id}", request);
        await response.HandleErrorAsync();
        return await response.Content.ReadFromJsonAsync<UnitDto>() ?? throw new Exception("Failed to update unit");
    }

    public async Task DeleteAsync(Guid id)
    {
        var response = await _httpClient.DeleteAsync($"api/units/{id}");
        await response.HandleErrorAsync();
    }

    public async Task<UnitDto> SetSellingDetailsAsync(Guid id, SetSellingDetailsRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/units/{id}/selling-details", request);
        await response.HandleErrorAsync();
        return await response.Content.ReadFromJsonAsync<UnitDto>() ?? throw new Exception("Failed to set selling details");
    }

    public async Task<UnitDto> SetLogisticsDetailsAsync(Guid id, SetLogisticsDetailsRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/units/{id}/logistics-details", request);
        await response.HandleErrorAsync();
        return await response.Content.ReadFromJsonAsync<UnitDto>() ?? throw new Exception("Failed to set logistics details");
    }
}
