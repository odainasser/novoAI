using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// Immutable audit log. Records are never updated, modified, or deleted.
/// </summary>
public class InventoryHistory : BaseEntity
{
    public Guid WarehouseId { get; set; }
    public virtual Warehouse Warehouse { get; set; } = null!;

    public Guid UnitId { get; set; }
    public virtual Unit Unit { get; set; } = null!;

    public InventoryActionType ActionType { get; set; }

    /// <summary>
    /// Positive for increases, negative for decreases.
    /// </summary>
    public int QuantityChange { get; set; }

    public int AvailableQuantityBefore { get; set; }
    public int AvailableQuantityAfter { get; set; }

    /// <summary>
    /// The type of source record (e.g., "GoodsReceivingNote", "StockAdjustment", "StockTake", "Order").
    /// </summary>
    public string ReferenceType { get; set; } = string.Empty;

    /// <summary>
    /// The ID of the source record that triggered this change.
    /// </summary>
    public Guid ReferenceId { get; set; }

    public string? PerformedBy { get; set; }
    public DateTime PerformedAt { get; set; }

    public string? Notes { get; set; }
}
