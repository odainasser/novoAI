using System.Net.Http.Json;
using System.Text.Json;
using Web.Models.Common;
using Web.Models.Categories;

namespace Web.Services;

public class ClientCategoryService : ICategoryService
{
    private readonly HttpClient _httpClient;

    public ClientCategoryService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PaginatedList<CategoryDto>> GetAllCategoriesAsync(int pageNumber, int pageSize, Guid? parentId = null, string? search = null, bool? isActive = null)
    {
        var url = $"api/categories?pageNumber={pageNumber}&pageSize={pageSize}";
        if (parentId.HasValue)
        {
            url += $"&parentId={parentId.Value}";
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            url += $"&search={Uri.EscapeDataString(search)}";
        }
        if (isActive.HasValue)
        {
            url += $"&isActive={isActive.Value}";
        }
        return await _httpClient.GetFromJsonAsync<PaginatedList<CategoryDto>>(url)
               ?? new PaginatedList<CategoryDto>(new List<CategoryDto>(), 0, pageNumber, pageSize);
    }

    public async Task<List<CategoryDto>> GetRootCategoriesAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<CategoryDto>>("api/categories/root")
               ?? new List<CategoryDto>();
    }

    public async Task<List<CategoryTreeDto>> GetCategoryTreeAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<CategoryTreeDto>>("api/categories/tree")
               ?? new List<CategoryTreeDto>();
    }

    public async Task<CategoryDto?> GetCategoryByIdAsync(Guid id)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<CategoryDto>($"api/categories/{id}");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<CategoryDto> CreateCategoryAsync(CreateCategoryRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/categories", request);
        await response.HandleErrorAsync();
        return await response.Content.ReadFromJsonAsync<CategoryDto>() ?? throw new Exception("Failed to create category");
    }

    public async Task<CategoryDto> UpdateCategoryAsync(Guid id, UpdateCategoryRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/categories/{id}", request);
        await response.HandleErrorAsync();
        return await response.Content.ReadFromJsonAsync<CategoryDto>() ?? throw new Exception("Failed to update category");
    }

    public async Task DeleteCategoryAsync(Guid id)
    {
        var response = await _httpClient.DeleteAsync($"api/categories/{id}");
        await response.HandleErrorAsync();
    }

    public async Task<bool> CheckCategoryExistsAsync(string nameEn, string nameAr, Guid? excludeCategoryId = null)
    {
        try
        {
            var url = $"api/categories/exists?nameEn={Uri.EscapeDataString(nameEn)}&nameAr={Uri.EscapeDataString(nameAr)}";
            if (excludeCategoryId.HasValue)
            {
                url += $"&excludeCategoryId={excludeCategoryId.Value}";
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
