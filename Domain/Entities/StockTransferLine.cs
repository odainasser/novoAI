using Domain.Common;

namespace Domain.Entities;

public class StockTransferLine : BaseAuditableEntity
{
    public Guid StockTransferId { get; set; }
    public virtual StockTransfer StockTransfer { get; set; } = null!;

    public Guid UnitId { get; set; }
    public virtual Unit Unit { get; set; } = null!;

    public int Quantity { get; set; }

    public int SourceQuantityBefore { get; set; }
    public int SourceQuantityAfter { get; set; }

    public int DestinationQuantityBefore { get; set; }
    public int DestinationQuantityAfter { get; set; }

    public string? Notes { get; set; }
}
