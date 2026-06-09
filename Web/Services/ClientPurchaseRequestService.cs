using System.Net;
using System.Net.Http.Json;
using Web.Models.Common;
using Web.Models.Enums;
using Web.Models.PurchaseRequests;

namespace Web.Services;

public class ClientPurchaseRequestService : IPurchaseRequestClientService
{
    private readonly HttpClient _httpClient;

    public ClientPurchaseRequestService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PaginatedList<PurchaseRequestDto>> GetAllAsync(
        int pageNumber, int pageSize, string? search = null,
        PurchaseRequestStatus? status = null, PurchaseRequestSupplySource? supplySource = null,
        Guid? warehouseId = null, DateTime? fromDate = null, DateTime? toDate = null, Guid? branchId = null)
    {
        var url = $"api/purchase-requests?pageNumber={pageNumber}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search)) url += $"&search={Uri.EscapeDataString(search)}";
        if (status.HasValue) url += $"&status={status.Value}";
        if (supplySource.HasValue) url += $"&supplySource={supplySource.Value}";
        if (warehouseId.HasValue) url += $"&warehouseId={warehouseId.Value}";
        if (fromDate.HasValue) url += $"&fromDate={fromDate.Value:yyyy-MM-dd}";
        if (toDate.HasValue) url += $"&toDate={toDate.Value:yyyy-MM-dd}";
        if (branchId.HasValue) url += $"&branchId={branchId.Value}";

        return await _httpClient.GetFromJsonAsync<PaginatedList<PurchaseRequestDto>>(url)
               ?? new PaginatedList<PurchaseRequestDto>(new List<PurchaseRequestDto>(), 0, pageNumber, pageSize);
    }

    public async Task<PurchaseRequestDto?> GetByIdAsync(Guid id, Guid? branchId = null)
    {
        try
        {
            var url = $"api/purchase-requests/{id}";
            if (branchId.HasValue) url += $"?branchId={branchId.Value}";
            return await _httpClient.GetFromJsonAsync<PurchaseRequestDto>(url);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<PurchaseRequestDto> CreateAsync(CreatePurchaseRequestRequest request, Guid? branchId = null)
    {
        var url = "api/purchase-requests";
        if (branchId.HasValue) url += $"?branchId={branchId.Value}";
        var response = await _httpClient.PostAsJsonAsync(url, request);
        await response.HandleErrorAsync();
        return await response.Content.ReadFromJsonAsync<PurchaseRequestDto>()
               ?? throw new Exception("Failed to create purchase request");
    }

    public async Task<PurchaseRequestDto?> UpdateAsync(Guid id, UpdatePurchaseRequestRequest request, Guid? branchId = null)
    {
        var url = $"api/purchase-requests/{id}";
        if (branchId.HasValue) url += $"?branchId={branchId.Value}";
        var response = await _httpClient.PutAsJsonAsync(url, request);
        await response.HandleErrorAsync();
        return await response.Content.ReadFromJsonAsync<PurchaseRequestDto>();
    }

    public async Task<int> GenerateAutoReorderProposalsAsync(Guid? warehouseId = null, Guid? branchId = null)
    {
        var url = "api/purchase-requests/auto-reorder";
        var qs = new List<string>();
        if (warehouseId.HasValue) qs.Add($"warehouseId={warehouseId.Value}");
        if (branchId.HasValue) qs.Add($"branchId={branchId.Value}");
        if (qs.Count > 0) url += "?" + string.Join("&", qs);

        var response = await _httpClient.PostAsync(url, null);
        await response.HandleErrorAsync();
        var result = await response.Content.ReadFromJsonAsync<AutoReorderResult>();
        return result?.Created ?? 0;
    }

    public async Task<PurchaseRequestDto?> SubmitAsync(Guid id, Guid? branchId = null)
        => await PostActionAsync($"{id}/submit", branchId, body: null);

    public async Task<PurchaseRequestDto?> ApproveAsync(Guid id, string? note = null)
        => await PostActionAsync($"{id}/approve", null, new ReviewPurchaseRequestRequest { Note = note });

    public async Task<PurchaseRequestDto?> RejectAsync(Guid id, string? reason = null)
        => await PostActionAsync($"{id}/reject", null, new ReviewPurchaseRequestRequest { Note = reason });

    public async Task<PurchaseRequestDto?> ConvertAsync(Guid id)
        => await PostActionAsync($"{id}/convert", null, body: null);

    public async Task<PurchaseRequestDto?> CancelAsync(Guid id, Guid? branchId = null)
        => await PostActionAsync($"{id}/cancel", branchId, body: null);

    private async Task<PurchaseRequestDto?> PostActionAsync(string path, Guid? branchId, ReviewPurchaseRequestRequest? body)
    {
        var url = $"api/purchase-requests/{path}";
        if (branchId.HasValue) url += $"?branchId={branchId.Value}";
        var response = body is null
            ? await _httpClient.PostAsync(url, null)
            : await _httpClient.PostAsJsonAsync(url, body);
        await response.HandleErrorAsync();
        return await response.Content.ReadFromJsonAsync<PurchaseRequestDto>();
    }

    private sealed class AutoReorderResult
    {
        public int Created { get; set; }
    }
}
