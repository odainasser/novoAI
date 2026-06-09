using System.Net.Http.Json;
using System.Text.Json;
using Web.Models.Cashiers;
using Web.Models.Common;

namespace Web.Services;

public class CashierManagementService : ICashierManagementService
{
    private readonly HttpClient _httpClient;

    public CashierManagementService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PaginatedList<CashierResponse>> GetAllCashiersAsync(int pageNumber, int pageSize, string? search = null, bool? isActive = null, Guid? warehouseId = null)
    {
        var url = $"api/cashiers?pageNumber={pageNumber}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search))
            url += $"&search={Uri.EscapeDataString(search)}";
        if (isActive.HasValue)
            url += $"&isActive={isActive.Value}";
        if (warehouseId.HasValue)
            url += $"&warehouseId={warehouseId.Value}";
        return await _httpClient.GetFromJsonAsync<PaginatedList<CashierResponse>>(url)
               ?? new PaginatedList<CashierResponse>(new List<CashierResponse>(), 0, pageNumber, pageSize);
    }

    public async Task<CashierResponse?> GetCashierByIdAsync(Guid cashierId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<CashierResponse>($"api/cashiers/{cashierId}");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<CashierResponse?> GetCurrentCashierProfileAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<CashierResponse>("api/cashiers/me");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound || ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            return null;
        }
    }

    public async Task<CashierResponse> CreateCashierAsync(CreateCashierRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/cashiers", request);
        await response.HandleErrorAsync();
        return await response.Content.ReadFromJsonAsync<CashierResponse>() ?? throw new Exception("Failed to create cashier");
    }

    public async Task<CashierResponse> UpdateCashierAsync(Guid cashierId, UpdateCashierRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/cashiers/{cashierId}", request);
        await response.HandleErrorAsync();
        return await response.Content.ReadFromJsonAsync<CashierResponse>() ?? throw new Exception("Failed to update cashier");
    }

    public async Task DeleteCashierAsync(Guid cashierId)
    {
        var response = await _httpClient.DeleteAsync($"api/cashiers/{cashierId}");
        await response.HandleErrorAsync();
    }

    public async Task<bool> CheckEmailExistsAsync(string email)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<JsonElement>($"api/cashiers/exists?email={Uri.EscapeDataString(email)}");
            return response.GetProperty("exists").GetBoolean();
        }
        catch
        {
            return false;
        }
    }

    public async Task<CashierResponse> SwitchMyStoreAsync(Guid warehouseId)
    {
        var request = new SwitchStoreRequest { WarehouseId = warehouseId };
        var response = await _httpClient.PostAsJsonAsync("api/cashiers/me/switch-store", request);
        await response.HandleErrorAsync();
        return await response.Content.ReadFromJsonAsync<CashierResponse>() ?? throw new Exception("Failed to switch store");
    }

    public async Task<List<AssignedWarehouseDto>> GetMyAssignedStoresAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<AssignedWarehouseDto>>("api/cashiers/me/stores") ?? new();
        }
        catch
        {
            return new();
        }
    }
}
