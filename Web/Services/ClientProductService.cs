using System.Net.Http.Json;
using System.Text.Json;
using Web.Models.Common;
using Web.Models.Enums;
using Web.Models.Products;

namespace Web.Services;

public class ClientProductService : IProductService
{
    private readonly HttpClient _httpClient;

    public ClientProductService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PaginatedList<ProductDto>> GetAllProductsAsync(int pageNumber, int pageSize, string? search = null, Guid? categoryId = null, bool? isActive = null, Guid? warehouseId = null, bool? onlyWithStock = null, ItemStatus? status = null)
    {
        var url = $"api/products?pageNumber={pageNumber}&pageSize={pageSize}";

        if (!string.IsNullOrWhiteSpace(search))
            url += $"&search={Uri.EscapeDataString(search)}";
        if (categoryId.HasValue)
            url += $"&categoryId={categoryId.Value}";
        if (status.HasValue)
            url += $"&status={(int)status.Value}";
        else if (isActive.HasValue)
            url += $"&isActive={isActive.Value}";
        if (warehouseId.HasValue)
            url += $"&warehouseId={warehouseId.Value}";
        if (onlyWithStock.HasValue)
            url += $"&onlyWithStock={onlyWithStock.Value}";
        return await _httpClient.GetFromJsonAsync<PaginatedList<ProductDto>>(url)
               ?? new PaginatedList<ProductDto>(new List<ProductDto>(), 0, pageNumber, pageSize);
    }

    public async Task<ProductDetailDto?> GetProductByIdAsync(Guid id)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<ProductDetailDto>($"api/products/{id}");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<ProductDto?> GetProductByCodeAsync(string code)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<ProductDto>($"api/products/code/{Uri.EscapeDataString(code)}");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<ProductDto> CreateProductAsync(CreateProductRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/products", request);
        await response.HandleErrorAsync();
        return await response.Content.ReadFromJsonAsync<ProductDto>() ?? throw new Exception("Failed to create product");
    }

    public async Task<ProductDto> UpdateProductAsync(Guid id, UpdateProductRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/products/{id}", request);
        await response.HandleErrorAsync();
        return await response.Content.ReadFromJsonAsync<ProductDto>() ?? throw new Exception("Failed to update product");
    }

    public async Task DeleteProductAsync(Guid id)
    {
        var response = await _httpClient.DeleteAsync($"api/products/{id}");
        await response.HandleErrorAsync();
    }

    public async Task<bool> CheckCodeExistsAsync(string code, Guid? excludeProductId = null)
    {
        try
        {
            var url = $"api/products/code-exists/{Uri.EscapeDataString(code)}";
            if (excludeProductId.HasValue)
            {
                url += $"?excludeProductId={excludeProductId.Value}";
            }
            var response = await _httpClient.GetFromJsonAsync<JsonElement>(url);
            return response.GetProperty("exists").GetBoolean();
        }
        catch
        {
            return false;
        }
    }

}
