using Application.Common.Models;
using Application.Features.Orders;
using Domain.Enums;

namespace Application.Services;

public interface IOrderService
{
    Task<PaginatedList<OrderDto>> GetAllOrdersAsync(
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
        IEnumerable<Guid>? warehouseIds = null);
    
    Task<PaginatedList<OrderDto>> GetOrdersByCashierAsync(
        Guid cashierId,
        int pageNumber,
        int pageSize,
        string? search = null,
        OrderStatus? status = null,
        PaymentMethod? paymentMethod = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        Guid? warehouseId = null);

    Task<OrderDto?> GetOrderByIdAsync(Guid id);
    Task<OrderDto?> GetOrderByNumberAsync(string orderNumber);
    Task<OrderDto> CreateOrderAsync(CreateOrderRequest request, Guid? cashierId = null, string? cashierName = null);
    Task<OrderDto> UpdateOrderStatusAsync(Guid id, UpdateOrderStatusRequest request);
    Task<bool> CancelOrderAsync(Guid id, string? reason);
    Task<bool> PartialRefundAsync(Guid id, List<PartialRefundItem> items, DateTime? clientCreatedAt = null);
    
    Task<string> GenerateOrderNumberAsync();
    Task<OrderStatisticsDto> GetOrderStatisticsAsync(Guid? cashierId = null, DateTime? fromDate = null, DateTime? toDate = null);

    Task<byte[]> ExportOrdersToExcelAsync(
        string? search = null,
        OrderStatus? status = null,
        OrderChannel? channel = null,
        PaymentMethod? paymentMethod = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        Guid? warehouseId = null,
        Guid? cashierId = null,
        bool isArabic = false,
        IEnumerable<Guid>? warehouseIds = null);
}

public class PartialRefundItem
{
    public Guid OrderItemId { get; set; }
    public int Quantity { get; set; }
}

public class OrderStatisticsDto
{
    public int TotalOrders { get; set; }
    public int CompletedOrders { get; set; }
    public int CancelledOrders { get; set; }
    public int PendingOrders { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TodayRevenue { get; set; }
    public int TodayOrders { get; set; }
    public int TodayItemsSold { get; set; }
}
