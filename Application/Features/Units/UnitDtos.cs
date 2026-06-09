using Domain.Enums;

namespace Application.Features.Units;

public class UnitDto
{
    public Guid Id { get; set; }
    public Guid UnitOfMeasureId { get; set; }
    public string? UnitOfMeasureNameEn { get; set; }
    public string? UnitOfMeasureNameAr { get; set; }
    public List<UnitTypeItemDto> UnitTypes { get; set; } = new();
    public Guid ProductId { get; set; }
    public string? ProductNameEn { get; set; }
    public string? ProductNameAr { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int LowStockThreshold { get; set; }
    public decimal SellingPrice { get; set; }
    public string SellingBarcode { get; set; } = string.Empty;
    public decimal Cost { get; set; }
    public List<UnitSupplierDto> Suppliers { get; set; } = new();
    public ItemStatus Status { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class UnitTypeItemDto
{
    public Guid Id { get; set; }
    public string? Code { get; set; }
    public string? NameEn { get; set; }
    public string? NameAr { get; set; }
}

public class CreateUnitRequest
{
    public Guid UnitOfMeasureId { get; set; }
    public List<Guid> UnitTypeIds { get; set; } = new();
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public int LowStockThreshold { get; set; } = 10;
    public ItemStatus? Status { get; set; }
}

public class UpdateUnitRequest
{
    public Guid UnitOfMeasureId { get; set; }
    public List<Guid> UnitTypeIds { get; set; } = new();
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public int LowStockThreshold { get; set; } = 10;
    // Status is managed via the Requests workflow; use ItemStatus.Inactive to deactivate
    public ItemStatus? Status { get; set; }
}

public class SetSellingDetailsRequest
{
    public decimal SellingPrice { get; set; }
    public string SellingBarcode { get; set; } = string.Empty;
    public int LowStockThreshold { get; set; } = 10;
}

public class SetLogisticsDetailsRequest
{
    public decimal Cost { get; set; }
    public List<UnitSupplierItem> Suppliers { get; set; } = new();
    public int LowStockThreshold { get; set; } = 10;
}

public class UnitSupplierItem
{
    public Guid SupplierId { get; set; }
    public string Barcode { get; set; } = string.Empty;
}

public class UnitSupplierDto
{
    public Guid SupplierId { get; set; }
    public string? SupplierNameEn { get; set; }
    public string? SupplierNameAr { get; set; }
    public string Barcode { get; set; } = string.Empty;
}
