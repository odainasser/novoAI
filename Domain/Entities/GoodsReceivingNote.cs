using Domain.Common;

namespace Domain.Entities;

public class GoodsReceivingNote : BaseAuditableEntity
{
    public string GRNNumber { get; set; } = string.Empty;

    public Guid? SupplierId { get; set; }
    public virtual Supplier? Supplier { get; set; }

    public Guid WarehouseId { get; set; }
    public virtual Warehouse Warehouse { get; set; } = null!;

    public string? PurchaseOrderReference { get; set; }

    public string? ReceivedBy { get; set; }
    public DateTime? ReceivedDate { get; set; }

    public Guid? RequestedById { get; set; }
    public string? RequestedByName { get; set; }

    /// <summary>Set when this GRN was created by converting a Purchase Request.</summary>
    public Guid? PurchaseRequestId { get; set; }

    public int TotalItems { get; set; }

    public string? Notes { get; set; }
    public string? AttachmentPath { get; set; }

    public virtual ICollection<GoodsReceivingNoteLine> Lines { get; set; } = new List<GoodsReceivingNoteLine>();
}
