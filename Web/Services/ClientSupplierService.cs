using System.Net.Http.Json;
using System.Text.Json;
using Web.Models.Common;
using Web.Models.Suppliers;

namespace Web.Services;

public class ClientSupplierService : ISupplierService
{
    private readonly HttpClient _httpClient;

    public ClientSupplierService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PaginatedList<SupplierDto>> GetAllSuppliersAsync(int pageNumber, int pageSize, string? search = null, bool? isActive = null)
    {
        var url = $"api/suppliers?pageNumber={pageNumber}&pageSize={pageSize}";
        
        if (!string.IsNullOrWhiteSpace(search))
        {
            url += $"&search={Uri.EscapeDataString(search)}";
        }
        if (isActive.HasValue)
        {
            url += $"&isActive={isActive.Value}";
        }
        
        return await _httpClient.GetFromJsonAsync<PaginatedList<SupplierDto>>(url)
               ?? new PaginatedList<SupplierDto>(new List<SupplierDto>(), 0, pageNumber, pageSize);
    }

    public async Task<SupplierDto?> GetSupplierByIdAsync(Guid id)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<SupplierDto>($"api/suppliers/{id}");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<SupplierDto> CreateSupplierAsync(CreateSupplierRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/suppliers", request);
        await response.HandleErrorAsync();
        return await response.Content.ReadFromJsonAsync<SupplierDto>() ?? throw new Exception("Failed to create supplier");
    }

    public async Task<SupplierDto> UpdateSupplierAsync(Guid id, UpdateSupplierRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/suppliers/{id}", request);
        await response.HandleErrorAsync();
        return await response.Content.ReadFromJsonAsync<SupplierDto>() ?? throw new Exception("Failed to update supplier");
    }

    public async Task DeleteSupplierAsync(Guid id)
    {
        var response = await _httpClient.DeleteAsync($"api/suppliers/{id}");
        await response.HandleErrorAsync();
    }

    public async Task<bool> CheckSupplierExistsAsync(string nameEn, string nameAr, Guid? excludeSupplierId = null)
    {
        try
        {
            var url = $"api/suppliers/exists?nameEn={Uri.EscapeDataString(nameEn)}&nameAr={Uri.EscapeDataString(nameAr)}";
            if (excludeSupplierId.HasValue)
            {
                url += $"&excludeSupplierId={excludeSupplierId.Value}";
            }
            var response = await _httpClient.GetFromJsonAsync<JsonElement>(url);
            return response.GetProperty("exists").GetBoolean();
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> CheckSupplierEmailExistsAsync(string email, Guid? excludeSupplierId = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            var url = $"api/suppliers/email-exists?email={Uri.EscapeDataString(email)}";
            if (excludeSupplierId.HasValue)
            {
                url += $"&excludeSupplierId={excludeSupplierId.Value}";
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
