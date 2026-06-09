using Domain.Enums;

namespace Application.Features.Products;

public class ProductDto
{
    public Guid Id { get; set; }
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? DescriptionEn { get; set; }
    public string? DescriptionAr { get; set; }
    public string Code { get; set; } = string.Empty;
    public int TotalStock { get; set; }
    public Guid? CategoryId { get; set; }
    public string? CategoryNameEn { get; set; }
    public string? CategoryNameAr { get; set; }
    public string? ImageUrl { get; set; }
    public ItemStatus Status { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    

    
    // Order statistics
    public int TotalSold { get; set; }
    public int TotalRefunded { get; set; }
    
    // Units
    public List<ProductUnitDto> Units { get; set; } = new();
}

public class ProductUnitDto
{
    public Guid Id { get; set; }
    public Guid UnitOfMeasureId { get; set; }
    public string? UnitOfMeasureNameEn { get; set; }
    public string? UnitOfMeasureNameAr { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int LowStockThreshold { get; set; }
    public decimal SellingPrice { get; set; }
    public bool IsActive { get; set; }
    /// <summary>Available stock for this unit (base units), summed across the scoped warehouse(s).</summary>
    public int AvailableQuantity { get; set; }
}

public class ProductStockDetailDto
{
    public Guid WarehouseId { get; set; }
    public string WarehouseNameEn { get; set; } = string.Empty;
    public string WarehouseNameAr { get; set; } = string.Empty;
    public string WarehouseType { get; set; } = string.Empty;
    public int AvailableQuantity { get; set; }
    public int ReservedQuantity { get; set; }
    public int InTransitQuantity { get; set; }
    public int Total => AvailableQuantity + ReservedQuantity + InTransitQuantity;
}

public class ProductStockMovementDto
{
    public DateTime PerformedAt { get; set; }
    public string WarehouseNameEn { get; set; } = string.Empty;
    public string WarehouseNameAr { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public int QuantityChange { get; set; }
    public int AvailableQuantityAfter { get; set; }
    public string? PerformedBy { get; set; }
}

public class ProductDetailDto : ProductDto
{
    public List<ProductStockDetailDto> StockByLocation { get; set; } = new();
    public List<ProductStockMovementDto> RecentStockMovements { get; set; } = new();
}

public class CreateProductRequest
{
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? DescriptionEn { get; set; }
    public string? DescriptionAr { get; set; }
    public string Code { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public ItemStatus? Status { get; set; }
}

public class UpdateProductRequest
{
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? DescriptionEn { get; set; }
    public string? DescriptionAr { get; set; }
    public string Code { get; set; } = string.Empty;
    public Guid? CategoryId { get; set; }
    // Status is managed via the Requests workflow; use ItemStatus.Inactive to deactivate
    public ItemStatus? Status { get; set; }
    public List<Guid> RemovedImageIds { get; set; } = new();
    public Guid? MainImageId { get; set; }
}
