using Domain.Common;

namespace Domain.Entities;

public class GoodsReceivingNoteLine : BaseAuditableEntity
{
    public Guid GoodsReceivingNoteId { get; set; }
    public virtual GoodsReceivingNote GoodsReceivingNote { get; set; } = null!;

    public Guid UnitId { get; set; }
    public virtual Unit Unit { get; set; } = null!;

    public Guid SupplierId { get; set; }
    public virtual Supplier Supplier { get; set; } = null!;

    public decimal Cost { get; set; }

    public int ReceivedQuantity { get; set; }

    public string? Notes { get; set; }
}
