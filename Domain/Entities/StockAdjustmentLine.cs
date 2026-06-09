using Domain.Common;

namespace Domain.Entities;

public class StockAdjustmentLine : BaseAuditableEntity
{
    public Guid StockAdjustmentId { get; set; }
    public virtual StockAdjustment StockAdjustment { get; set; } = null!;

    public Guid UnitId { get; set; }
    public virtual Unit Unit { get; set; } = null!;

    /// <summary>
    /// Snapshot of available quantity at the time of submission. Locked after submission.
    /// </summary>
    public int CurrentQuantity { get; set; }

    /// <summary>
    /// Positive for additions, positive value for removals (direction determined by AdjustmentType).
    /// </summary>
    public int AdjustmentQuantity { get; set; }

    /// <summary>
    /// Computed: CurrentQuantity +/- AdjustmentQuantity based on type.
    /// </summary>
    public int NewQuantity { get; set; }

    public string? Notes { get; set; }
}
