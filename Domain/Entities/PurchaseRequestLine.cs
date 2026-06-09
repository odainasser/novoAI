using Domain.Common;

namespace Domain.Entities;

public class PurchaseRequestLine : BaseAuditableEntity
{
    public Guid PurchaseRequestId { get; set; }
    public virtual PurchaseRequest PurchaseRequest { get; set; } = null!;

    public Guid UnitId { get; set; }
    public virtual Unit Unit { get; set; } = null!;

    public int RequestedQuantity { get; set; }

    /// <summary>Filled by auto-reorder; null for manual lines.</summary>
    public int? SuggestedQuantity { get; set; }

    /// <summary>Snapshot of available quantity at the requesting warehouse when the line was created.</summary>
    public int CurrentAvailableQuantity { get; set; }

    public string? Notes { get; set; }
}
