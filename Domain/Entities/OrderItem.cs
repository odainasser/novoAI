using Domain.Common;

namespace Domain.Entities;

public class OrderItem : BaseAuditableEntity
{
    public Guid OrderId { get; set; }
    public virtual Order Order { get; set; } = null!;
    
    public Guid ProductId { get; set; }
    public virtual Product Product { get; set; } = null!;
    
    public Guid? UnitId { get; set; }
    public virtual Unit? Unit { get; set; }
    
    public string ProductNameEn { get; set; } = string.Empty;
    public string ProductNameAr { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    
    // Unit snapshot (preserved at time of sale)
    public string? UnitNameEn { get; set; }
    public string? UnitNameAr { get; set; }
    public string? UnitBarcode { get; set; }
    
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total { get; set; }
}
