using Web.Models.Common;
using Web.Models.Orders;
using Web.Models.Enums;

namespace Web.Services;
using Web.Models.Orders;

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
        Guid? branchId = null);

    Task<OrderDto?> GetOrderByIdAsync(Guid id);
    Task<OrderDto?> GetOrderByNumberAsync(string orderNumber);
    Task<OrderDto> CreateOrderAsync(CreateOrderRequest request);
    Task<OrderDto> UpdateOrderStatusAsync(Guid id, UpdateOrderStatusRequest request);
    Task<OrderDto?> CancelOrderAsync(Guid id, string? reason);
    Task<OrderDto?> PartialRefundAsync(Guid id, List<PartialRefundItemRequest> items);
    Task<OrderDto?> PartialRefundAsync(Guid id, decimal amount);
    
    Task<OrderStatisticsDto> GetOrderStatisticsAsync(DateTime? fromDate = null, DateTime? toDate = null);

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
        Guid? branchId = null);
}
