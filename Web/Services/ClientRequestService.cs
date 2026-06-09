using System.Net.Http.Json;
using Web.Models.Common;
using Web.Models.Enums;
using Web.Models.Requests;

namespace Web.Services;

public class ClientRequestService : IRequestService
{
    private readonly HttpClient _httpClient;

    public ClientRequestService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PaginatedList<RequestDto>> GetAllRequestsAsync(
        int pageNumber,
        int pageSize,
        string? search = null,
        RequestType? type = null,
        RequestStatus? status = null,
        Guid? branchId = null)
    {
        var url = $"api/requests?pageNumber={pageNumber}&pageSize={pageSize}";

        if (!string.IsNullOrWhiteSpace(search))
            url += $"&search={Uri.EscapeDataString(search)}";
        if (type.HasValue)
            url += $"&type={type.Value}";
        if (status.HasValue)
            url += $"&status={status.Value}";
        if (branchId.HasValue)
            url += $"&branchId={branchId.Value}";

        return await _httpClient.GetFromJsonAsync<PaginatedList<RequestDto>>(url)
               ?? new PaginatedList<RequestDto>(new List<RequestDto>(), 0, pageNumber, pageSize);
    }

    public async Task<RequestDto?> GetRequestByIdAsync(Guid id)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<RequestDto>($"api/requests/{id}");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<RequestDto?> GetPendingProductUpdateRequestAsync(Guid productId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<RequestDto>($"api/requests/pending-product-update/{productId}");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<RequestDto> CreateSetUnitPriceRequestAsync(CreateSetUnitPriceRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/requests/set-unit-price", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RequestDto>()
               ?? throw new Exception("Failed to create request");
    }

    public async Task<RequestDto> CreateActivateProductRequestAsync(CreateActivateProductRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/requests/activate-product", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RequestDto>()
               ?? throw new Exception("Failed to create request");
    }

    public async Task<RequestDto> CreateActivateUnitRequestAsync(CreateActivateUnitRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/requests/activate-unit", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RequestDto>()
               ?? throw new Exception("Failed to create request");
    }

    public async Task<RequestDto?> ReviewRequestAsync(Guid id, ReviewRequestDto review)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/requests/{id}/review", review);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        await response.HandleErrorAsync();
        return await response.Content.ReadFromJsonAsync<RequestDto>();
    }

    public async Task<RequestDto> CreateAddGRNRequestAsync(CreateInventoryGRNRequest request, Guid? branchId = null)
    {
        var url = "api/requests/add-grn";
        if (branchId.HasValue) url += $"?branchId={branchId.Value}";
        var response = await _httpClient.PostAsJsonAsync(url, request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RequestDto>()
               ?? throw new Exception("Failed to create request");
    }

    public async Task<RequestDto?> UpdateAddGRNRequestAsync(Guid id, CreateInventoryGRNRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/requests/{id}/add-grn", request);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        await response.HandleErrorAsync();
        return await response.Content.ReadFromJsonAsync<RequestDto>();
    }

    public async Task<RequestDto> CreateAddStockAdjustmentRequestAsync(CreateInventoryAdjustmentRequest request, Guid? branchId = null)
    {
        var url = "api/requests/add-stock-adjustment";
        if (branchId.HasValue) url += $"?branchId={branchId.Value}";
        var response = await _httpClient.PostAsJsonAsync(url, request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RequestDto>()
               ?? throw new Exception("Failed to create request");
    }

    public async Task<RequestDto> CreateAddStockTransferRequestAsync(CreateInventoryTransferRequest request, Guid? branchId = null)
    {
        var url = "api/requests/add-stock-transfer";
        if (branchId.HasValue) url += $"?branchId={branchId.Value}";
        var response = await _httpClient.PostAsJsonAsync(url, request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RequestDto>()
               ?? throw new Exception("Failed to create request");
    }

    public async Task<RequestDto> CreateDeleteProductRequestAsync(CreateDeleteProductRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/requests/delete-product", request);
        await response.HandleErrorAsync();
        return await response.Content.ReadFromJsonAsync<RequestDto>()
               ?? throw new Exception("Failed to create request");
    }

    public async Task<RequestDto> CreateDeleteUnitRequestAsync(CreateDeleteUnitRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/requests/delete-unit", request);
        await response.HandleErrorAsync();
        return await response.Content.ReadFromJsonAsync<RequestDto>()
               ?? throw new Exception("Failed to create request");
    }

    public async Task<RequestDto> CreateSetLogisticsDetailsRequestAsync(CreateSetLogisticsDetailsRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/requests/set-logistics-details", request);
        await response.HandleErrorAsync();
        return await response.Content.ReadFromJsonAsync<RequestDto>()
               ?? throw new Exception("Failed to create request");
    }

    public async Task<RequestDto?> GetPendingSetLogisticsDetailsRequestAsync(Guid unitId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<RequestDto>($"api/requests/pending-logistics/{unitId}");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<PaginatedList<RequestDto>> GetMyRequestsAsync(
        int pageNumber,
        int pageSize,
        string? search = null,
        RequestType? type = null,
        RequestStatus? status = null)
    {
        var url = $"api/requests/my?pageNumber={pageNumber}&pageSize={pageSize}";

        if (!string.IsNullOrWhiteSpace(search))
            url += $"&search={Uri.EscapeDataString(search)}";
        if (type.HasValue)
            url += $"&type={type.Value}";
        if (status.HasValue)
            url += $"&status={status.Value}";

        return await _httpClient.GetFromJsonAsync<PaginatedList<RequestDto>>(url)
               ?? new PaginatedList<RequestDto>(new List<RequestDto>(), 0, pageNumber, pageSize);
    }

    public async Task<bool> DeleteRequestAsync(Guid id)
    {
        var response = await _httpClient.DeleteAsync($"api/requests/{id}");
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteMyRequestAsync(Guid id)
    {
        var response = await _httpClient.DeleteAsync($"api/requests/my/{id}");
        return response.IsSuccessStatusCode;
    }
}
