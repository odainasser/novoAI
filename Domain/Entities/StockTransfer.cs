using Domain.Common;

namespace Domain.Entities;

public class StockTransfer : BaseAuditableEntity
{
    public string TransferNumber { get; set; } = string.Empty;

    public Guid FromWarehouseId { get; set; }
    public virtual Warehouse FromWarehouse { get; set; } = null!;

    public Guid ToWarehouseId { get; set; }
    public virtual Warehouse ToWarehouse { get; set; } = null!;

    public Guid? RequestedById { get; set; }
    public string? RequestedByName { get; set; }
    public DateTime? RequestedDate { get; set; }

    /// <summary>Set when this transfer was created by converting a Purchase Request.</summary>
    public Guid? PurchaseRequestId { get; set; }

    public string? Notes { get; set; }

    public virtual ICollection<StockTransferLine> Lines { get; set; } = new List<StockTransferLine>();
}
