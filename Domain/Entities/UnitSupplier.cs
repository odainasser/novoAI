namespace Domain.Entities;

public class UnitSupplier
{
    public Guid UnitId { get; set; }
    public virtual Unit? Unit { get; set; }
    public Guid SupplierId { get; set; }
    public virtual Supplier? Supplier { get; set; }
    public string Barcode { get; set; } = string.Empty;
}
