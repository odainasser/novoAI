using Domain.Common;

namespace Domain.Entities;

public class OrderRefund : BaseAuditableEntity
{
    public Guid OrderId { get; set; }
    public virtual Order Order { get; set; } = null!;

    public decimal Amount { get; set; }
    public string? Reason { get; set; }

    public virtual ICollection<OrderRefundItem> Items { get; set; } = new List<OrderRefundItem>();
}

public class OrderRefundItem : BaseAuditableEntity
{
    public Guid OrderRefundId { get; set; }
    public virtual OrderRefund OrderRefund { get; set; } = null!;

    public Guid OrderItemId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total { get; set; }
}
