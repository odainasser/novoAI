using Web.Models.Common;
using Web.Models.Orders;
using Web.Models.Enums;
using System.Net.Http.Json;

namespace Web.Services;

public class OrderClientService : IOrderService
{
    private readonly HttpClient _httpClient;

    public OrderClientService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<OrderDto?> PartialRefundAsync(Guid id, List<PartialRefundItemRequest> items)
    {
        var payload = new { Items = items };
        var response = await _httpClient.PostAsJsonAsync($"api/orders/{id}/partial-refund", payload);
        if (!response.IsSuccessStatusCode)
        {
            string body = string.Empty;
            try { body = await response.Content.ReadAsStringAsync(); } catch { }
            var msg = !string.IsNullOrWhiteSpace(body) ? body : response.ReasonPhrase ?? "Partial refund failed";
            throw new InvalidOperationException(msg);
        }

        try
        {
            return await response.Content.ReadFromJsonAsync<OrderDto>();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to parse partial refund response", ex);
        }
    }

    public async Task<PaginatedList<OrderDto>> GetAllOrdersAsync(
        int pageNumber,
        int pageSize,
        string? search = null,
        OrderStatus? status = null,
        OrderChannel? channel = null,
        PaymentMethod? paymentMethod = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        Guid? warehouseId = null,
        Guid? cashierId = null,
        Guid? branchId = null)
    {
        var queryParams = new List<string>
        {
            $"pageNumber={pageNumber}",
            $"pageSize={pageSize}"
        };

        if (!string.IsNullOrWhiteSpace(search))
            queryParams.Add($"search={Uri.EscapeDataString(search)}");
        if (status.HasValue)
            queryParams.Add($"status={status.Value}");
        if (channel.HasValue)
            queryParams.Add($"channel={channel.Value}");
        if (paymentMethod.HasValue)
            queryParams.Add($"paymentMethod={paymentMethod.Value}");
        if (fromDate.HasValue)
            queryParams.Add($"fromDate={Uri.EscapeDataString(fromDate.Value.ToString("o"))}");
        if (toDate.HasValue)
            queryParams.Add($"toDate={Uri.EscapeDataString(toDate.Value.ToString("o"))}");
        if (warehouseId.HasValue)
            queryParams.Add($"warehouseId={warehouseId.Value}");
        if (cashierId.HasValue)
            queryParams.Add($"cashierId={cashierId.Value}");
        if (branchId.HasValue)
            queryParams.Add($"branchId={branchId.Value}");

        var response = await _httpClient.GetAsync($"api/orders?{string.Join("&", queryParams)}");
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadFromJsonAsync<PaginatedList<OrderDto>>() 
            ?? new PaginatedList<OrderDto>(new List<OrderDto>(), 0, pageNumber, pageSize);
    }

    public async Task<OrderDto?> GetOrderByIdAsync(Guid id)
    {
        var response = await _httpClient.GetAsync($"api/orders/{id}");
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OrderDto>();
    }

    public async Task<OrderDto?> GetOrderByNumberAsync(string orderNumber)
    {
        var response = await _httpClient.GetAsync($"api/orders/number/{Uri.EscapeDataString(orderNumber)}");
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OrderDto>();
    }

    public async Task<OrderDto> CreateOrderAsync(CreateOrderRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/orders", request);
        await response.HandleErrorAsync();

        return await response.Content.ReadFromJsonAsync<OrderDto>()
            ?? throw new InvalidOperationException("Failed to create order");
    }

    public async Task<OrderDto> UpdateOrderStatusAsync(Guid id, UpdateOrderStatusRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/orders/{id}/status", request);
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadFromJsonAsync<OrderDto>() 
            ?? throw new InvalidOperationException("Failed to update order status");
    }

    public async Task<OrderDto?> CancelOrderAsync(Guid id, string? reason)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/orders/{id}/refund", new { Reason = reason });
        if (!response.IsSuccessStatusCode)
        {
            // Try to read server error message for better diagnostics
            string body = string.Empty;
            try { body = await response.Content.ReadAsStringAsync(); } catch { }
            var msg = !string.IsNullOrWhiteSpace(body) ? body : response.ReasonPhrase ?? "Refund failed";
            throw new InvalidOperationException(msg);
        }

        // Read updated order DTO from response; if parsing fails, throw to be handled by caller
        try
        {
            return await response.Content.ReadFromJsonAsync<OrderDto>();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to parse refund response", ex);
        }
    }

    public async Task<OrderDto?> PartialRefundAsync(Guid id, decimal amount)
    {
        // Legacy amount-based partial refund: forward as payload to API
        var response = await _httpClient.PostAsJsonAsync($"api/orders/{id}/partial-refund", new { Amount = amount });
        if (!response.IsSuccessStatusCode)
        {
            string body = string.Empty;
            try { body = await response.Content.ReadAsStringAsync(); } catch { }
            var msg = !string.IsNullOrWhiteSpace(body) ? body : response.ReasonPhrase ?? "Partial refund failed";
            throw new InvalidOperationException(msg);
        }

        try
        {
            return await response.Content.ReadFromJsonAsync<OrderDto>();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to parse partial refund response", ex);
        }
    }

    public Task<string> GenerateOrderNumberAsync()
    {
        // This is generated server-side
        throw new NotImplementedException("Order number is generated server-side");
    }

    public async Task<OrderStatisticsDto> GetOrderStatisticsAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var queryParams = new List<string>();
        
        if (fromDate.HasValue)
            queryParams.Add($"fromDate={Uri.EscapeDataString(fromDate.Value.ToString("o"))}");
        if (toDate.HasValue)
            queryParams.Add($"toDate={Uri.EscapeDataString(toDate.Value.ToString("o"))}");

        var query = queryParams.Count > 0 ? $"?{string.Join("&", queryParams)}" : "";
        var response = await _httpClient.GetAsync($"api/orders/statistics{query}");
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadFromJsonAsync<OrderStatisticsDto>() 
            ?? new OrderStatisticsDto();
    }

    public async Task<byte[]> ExportOrdersToExcelAsync(
        string? search = null,
        OrderStatus? status = null,
        OrderChannel? channel = null,
        PaymentMethod? paymentMethod = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        Guid? warehouseId = null,
        Guid? cashierId = null,
        bool isArabic = false,
        Guid? branchId = null)
    {
        var queryParams = new List<string>();

        if (!string.IsNullOrWhiteSpace(search))
            queryParams.Add($"search={Uri.EscapeDataString(search)}");
        if (status.HasValue)
            queryParams.Add($"status={status.Value}");
        if (channel.HasValue)
            queryParams.Add($"channel={channel.Value}");
        if (paymentMethod.HasValue)
            queryParams.Add($"paymentMethod={paymentMethod.Value}");
        if (fromDate.HasValue)
            queryParams.Add($"fromDate={Uri.EscapeDataString(fromDate.Value.ToString("o"))}");
        if (toDate.HasValue)
            queryParams.Add($"toDate={Uri.EscapeDataString(toDate.Value.ToString("o"))}");
        if (warehouseId.HasValue)
            queryParams.Add($"warehouseId={warehouseId.Value}");
        if (cashierId.HasValue)
            queryParams.Add($"cashierId={cashierId.Value}");
        if (branchId.HasValue)
            queryParams.Add($"branchId={branchId.Value}");
        if (isArabic)
            queryParams.Add("ar=true");

        var query = queryParams.Count > 0 ? $"?{string.Join("&", queryParams)}" : "";
        var response = await _httpClient.GetAsync($"api/orders/export{query}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync();
    }
}
