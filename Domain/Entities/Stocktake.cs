using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// Header of a physical-count session (Full stocktake or Cycle count). Records
/// system vs. counted quantities only; it never mutates <see cref="StockBalance"/>
/// until it is approved, at which point <see cref="StockAdjustment"/> records are
/// generated for every counted difference.
/// </summary>
public class Stocktake : BaseAuditableEntity
{
    public string StocktakeNumber { get; set; } = string.Empty;

    public Guid WarehouseId { get; set; }
    public virtual Warehouse Warehouse { get; set; } = null!;

    public StocktakeType Type { get; set; }

    /// <summary>How the in-scope lines are selected. <see cref="StocktakeScopeType.All"/> for Full.</summary>
    public StocktakeScopeType ScopeType { get; set; } = StocktakeScopeType.All;

    /// <summary>Category to count when <see cref="ScopeType"/> is <see cref="StocktakeScopeType.Category"/>.</summary>
    public Guid? ScopeCategoryId { get; set; }
    public virtual Category? ScopeCategory { get; set; }

    public StocktakeStatus Status { get; set; } = StocktakeStatus.Draft;

    public Guid? CreatedById { get; set; }
    public string? CreatedByName { get; set; }

    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public string? Notes { get; set; }

    public virtual ICollection<StocktakeLine> Lines { get; set; } = new List<StocktakeLine>();
}
