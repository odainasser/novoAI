namespace Web.Models.Dashboard;

public class WarehouseStockDto
{
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public string WarehouseNameAr { get; set; } = string.Empty;
    public Guid WarehouseTypeId { get; set; }
    public string WarehouseTypeCode { get; set; } = string.Empty;
    public string WarehouseTypeName { get; set; } = string.Empty;
    public string WarehouseTypeNameAr { get; set; } = string.Empty;
    public string? BranchNameEn { get; set; }
    public string? BranchNameAr { get; set; }
    public List<ProductStockDto> Products { get; set; } = new();
}

public class ProductStockDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductNameAr { get; set; } = string.Empty;
    public int AvailableQuantity { get; set; }
    public int ReservedQuantity { get; set; }
    public int InTransitQuantity { get; set; }
    public int ReceivedQuantity { get; set; }
    public int TransferredInQuantity { get; set; }
    public int TransferredOutQuantity { get; set; }
    public int LowStockThreshold { get; set; }
}

public class DashboardSummaryDto
{
    public int TotalProducts { get; set; }
    public int ActiveProducts { get; set; }
    public int LowStockProducts { get; set; }
    public int TotalCategories { get; set; }
    public int TotalSuppliers { get; set; }
    public int TotalUnits { get; set; }
    public int TotalWarehouses { get; set; }
    public int TotalStockItems { get; set; }
    public int OutOfStockItems { get; set; }
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
}
