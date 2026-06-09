using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// One counted unit within a <see cref="Stocktake"/>. Quantities are expressed in
/// unit-count (number of the line's unit), consistent with how
/// <see cref="StockAdjustmentLine.AdjustmentQuantity"/> is expressed, so an
/// approved difference maps directly onto a generated adjustment line.
/// </summary>
public class StocktakeLine : BaseAuditableEntity
{
    public Guid StocktakeId { get; set; }
    public virtual Stocktake Stocktake { get; set; } = null!;

    public Guid UnitId { get; set; }
    public virtual Unit Unit { get; set; } = null!;

    /// <summary>Snapshot of the available quantity (in unit-count) at the moment counting started.</summary>
    public int SystemQuantity { get; set; }

    /// <summary>Quantity physically counted by staff (in unit-count). Null until counted.</summary>
    public int? CountedQuantity { get; set; }

    /// <summary>Computed CountedQuantity - SystemQuantity (in unit-count). Zero until counted.</summary>
    public int Difference { get; set; }

    public StocktakeLineStatus LineStatus { get; set; } = StocktakeLineStatus.Pending;

    /// <summary>Adjustment type chosen by the manager at approval; null until then.</summary>
    public StockAdjustmentType? AdjustmentType { get; set; }

    /// <summary>The StockAdjustment generated for this line on approval; null until then.</summary>
    public Guid? GeneratedAdjustmentId { get; set; }
    public string? GeneratedAdjustmentNumber { get; set; }

    public string? Notes { get; set; }
}
