using Domain.Common;

namespace Domain.Entities;

public class StockBalance : BaseAuditableEntity
{
    public Guid WarehouseId { get; set; }
    public virtual Warehouse Warehouse { get; set; } = null!;

    public Guid UnitId { get; set; }
    public virtual Unit Unit { get; set; } = null!;

    public int AvailableQuantity { get; set; }
    public int ReservedQuantity { get; set; }
    public int InTransitQuantity { get; set; }

    public DateTime? LastStockCheckDate { get; set; }

    /// <summary>
    /// Optimistic concurrency token.
    /// </summary>
    public byte[] RowVersion { get; set; } = null!;
}
