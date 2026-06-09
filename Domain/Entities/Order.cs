using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class Order : BaseAuditableEntity
{
    public string OrderNumber { get; set; } = string.Empty;

    /// <summary>
    /// Client-supplied idempotency key. Lets the server detect and ignore
    /// duplicate submissions of the same sale (notably offline replays that
    /// re-POST after a queue-delete failure). Unique when present.
    /// </summary>
    public string? IdempotencyKey { get; set; }

    public OrderChannel Channel { get; set; }
    public OrderStatus Status { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    // For split (cash + card) payments
    public decimal? CashAmount { get; set; }
    public decimal? CardAmount { get; set; }

    public decimal Subtotal { get; set; }
    public decimal VatRate { get; set; } = 0.05m; // 5% VAT
    public decimal VatAmount { get; set; }
    public decimal Total { get; set; }
    
    public Guid? CashierId { get; set; }
    public string? CashierName { get; set; }
    
    public Guid? WarehouseId { get; set; }
    public string? WarehouseNameEn { get; set; }
    public string? WarehouseNameAr { get; set; }
    
    public string? CustomerName { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }
    
    public string? Notes { get; set; }
    
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    
    public virtual ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public virtual ICollection<OrderRefund> Refunds { get; set; } = new List<OrderRefund>();
}
