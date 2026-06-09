using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class Unit : BaseAuditableEntity
{
    public Guid UnitOfMeasureId { get; set; }
    public virtual Lookup? UnitOfMeasure { get; set; }
    public Guid ProductId { get; set; }
    public virtual Product? Product { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int LowStockThreshold { get; set; } = 10;
    public decimal SellingPrice { get; set; }
    public string SellingBarcode { get; set; } = string.Empty;
    public decimal Cost { get; set; }
    public ItemStatus Status { get; set; } = ItemStatus.Draft;
    public bool IsActive { get; set; } = false;
    public virtual ICollection<UnitUnitType> UnitUnitTypes { get; set; } = new List<UnitUnitType>();
    public virtual ICollection<UnitSupplier> UnitSuppliers { get; set; } = new List<UnitSupplier>();
}
