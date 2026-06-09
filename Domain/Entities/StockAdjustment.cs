using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class StockAdjustment : BaseAuditableEntity
{
    public string AdjustmentNumber { get; set; } = string.Empty;

    public Guid WarehouseId { get; set; }
    public virtual Warehouse Warehouse { get; set; } = null!;

    public StockAdjustmentType AdjustmentType { get; set; }
    public StockAdjustmentStatus Status { get; set; } = StockAdjustmentStatus.Completed;

    public Guid? RequestedById { get; set; }
    public string? RequestedByName { get; set; }
    public DateTime? RequestedDate { get; set; }
    public string? Explanation { get; set; }

    /// <summary>
    /// Set when this adjustment was generated automatically by approving a
    /// <see cref="Stocktake"/>; null for adjustments created directly. Lets the
    /// Adjustments tab show that the record originated from a stocktake.
    /// </summary>
    public Guid? StocktakeId { get; set; }
    public string? StocktakeNumber { get; set; }

    public virtual ICollection<StockAdjustmentLine> Lines { get; set; } = new List<StockAdjustmentLine>();
}
