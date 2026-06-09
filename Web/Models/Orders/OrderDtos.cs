using Web.Models.Enums;

namespace Web.Models.Orders;

public class OrderDto
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public OrderChannel Channel { get; set; }
    public string ChannelName => Channel.ToString();
    public OrderStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public PaymentMethod PaymentMethod { get; set; }
    public string PaymentMethodName => PaymentMethod.ToString();
    public decimal? CashAmount { get; set; }
    public decimal? CardAmount { get; set; }
    
    public decimal Subtotal { get; set; }
    public decimal VatRate { get; set; }
    public decimal VatAmount { get; set; }
    public decimal Total { get; set; }

    public Guid? WarehouseId { get; set; }
    public string? WarehouseNameEn { get; set; }
    public string? WarehouseNameAr { get; set; }
    
    public string? CustomerName { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }
    
    public string? Notes { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    
    public int ItemCount { get; set; }
    public List<OrderItemDto> Items { get; set; } = new();
    public List<OrderRefundDto> Refunds { get; set; } = new();
}

public class OrderRefundDto
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Reason { get; set; }
    public List<OrderRefundItemDto> Items { get; set; } = new();
}

public class OrderRefundItemDto
{
    public Guid Id { get; set; }
    public Guid OrderItemId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total { get; set; }
}

public class PartialRefundItemRequest
{
    public Guid OrderItemId { get; set; }
    public int Quantity { get; set; }
}


public class OrderItemDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductNameEn { get; set; } = string.Empty;
    public string ProductNameAr { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public Guid? UnitId { get; set; }
    public string? UnitNameEn { get; set; }
    public string? UnitNameAr { get; set; }
    public string? UnitBarcode { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total { get; set; }
}

public class CreateOrderRequest
{
    public OrderChannel Channel { get; set; } = OrderChannel.POS;
    public PaymentMethod PaymentMethod { get; set; }
    // Required when PaymentMethod == Split
    public decimal? CashAmount { get; set; }
    public decimal? CardAmount { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }
    public string? Notes { get; set; }
    public List<CreateOrderItemRequest> Items { get; set; } = new();
    // Idempotency key — set to the local order id before enqueueing so a replay
    // that re-POSTs (after a failed queue-delete) is de-duplicated server-side.
    public string? IdempotencyKey { get; set; }
    // Offline replay only — captured before enqueueing so the server records
    // the actual sale time on replay rather than the sync moment.
    public DateTime? ClientCreatedAt { get; set; }
}

public class CreateOrderItemRequest
{
    public Guid ProductId { get; set; }
    public Guid UnitId { get; set; }
    public int Quantity { get; set; }
}

public class UpdateOrderStatusRequest
{
    public OrderStatus Status { get; set; }
    public string? CancellationReason { get; set; }
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
