namespace Application.Features.Inventory;

// ===== Stock Balance =====

public class StockBalanceDto
{
    public Guid Id { get; set; }
    public Guid WarehouseId { get; set; }
    public string WarehouseNameEn { get; set; } = string.Empty;
    public string WarehouseNameAr { get; set; } = string.Empty;
    public Guid ProductId { get; set; }
    public Guid UnitId { get; set; }
    public string UnitBarcode { get; set; } = string.Empty;
    public string ProductNameEn { get; set; } = string.Empty;
    public string ProductNameAr { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string UnitOfMeasureNameEn { get; set; } = string.Empty;
    public string UnitOfMeasureNameAr { get; set; } = string.Empty;
    public List<string> UnitTypesEn { get; set; } = new();
    public List<string> UnitTypesAr { get; set; } = new();
    public int UnitBaseQuantity { get; set; } = 1;
    public int AvailableQuantity { get; set; }
    public int ReservedQuantity { get; set; }
    public int InTransitQuantity { get; set; }
    public int LowStockThreshold { get; set; }
    public DateTime? LastStockCheckDate { get; set; }
}

// ===== Goods Receiving Note =====

public class GoodsReceivingNoteDto
{
    public Guid Id { get; set; }
    public string GRNNumber { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseNameEn { get; set; } = string.Empty;
    public string WarehouseNameAr { get; set; } = string.Empty;
    public string? PurchaseOrderReference { get; set; }
    public string? ReceivedBy { get; set; }    public Guid? RequestedById { get; set; }    public string? RequestedBy { get; set; }    public DateTime? ReceivedDate { get; set; }
    public int TotalItems { get; set; }
    public string? Notes { get; set; }
    public string? AttachmentPath { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<GoodsReceivingNoteLineDto> Lines { get; set; } = new();
}

public class GoodsReceivingNoteLineDto
{
    public Guid Id { get; set; }
    public Guid UnitId { get; set; }
    public string UnitBarcode { get; set; } = string.Empty;
    public string ProductNameEn { get; set; } = string.Empty;
    public string ProductNameAr { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string UnitOfMeasureNameEn { get; set; } = string.Empty;
    public string UnitOfMeasureNameAr { get; set; } = string.Empty;
    public int UnitBaseQuantity { get; set; } = 1;
    public Guid SupplierId { get; set; }
    public string SupplierNameEn { get; set; } = string.Empty;
    public string SupplierNameAr { get; set; } = string.Empty;
    public decimal Cost { get; set; }
    public int ReceivedQuantity { get; set; }
    public string? Notes { get; set; }
}

public class CreateGoodsReceivingNoteRequest
{
    public string? PurchaseOrderReference { get; set; }
    public DateTime? ReceivedDate { get; set; }
    public string? Notes { get; set; }
    /// <summary>Set when this GRN is created by converting a Purchase Request.</summary>
    public Guid? PurchaseRequestId { get; set; }
    public List<CreateGoodsReceivingNoteLineRequest> Lines { get; set; } = new();
}

public class CreateGoodsReceivingNoteLineRequest
{
    public Guid UnitId { get; set; }
    public Guid SupplierId { get; set; }
    public decimal Cost { get; set; }
    public int ReceivedQuantity { get; set; }
    public string? Notes { get; set; }
}

// ===== Stock Adjustment =====

public class StockAdjustmentDto
{
    public Guid Id { get; set; }
    public string AdjustmentNumber { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseNameEn { get; set; } = string.Empty;
    public string WarehouseNameAr { get; set; } = string.Empty;
    public string AdjustmentType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Guid? RequestedById { get; set; }
    public string? RequestedByName { get; set; }
    public DateTime? RequestedDate { get; set; }
    public string? Explanation { get; set; }
    public string? JustificationImageUrl { get; set; }
    public Guid? StocktakeId { get; set; }
    public string? StocktakeNumber { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<StockAdjustmentLineDto> Lines { get; set; } = new();
}

public class StockAdjustmentLineDto
{
    public Guid Id { get; set; }
    public Guid UnitId { get; set; }
    public string UnitBarcode { get; set; } = string.Empty;
    public string ProductNameEn { get; set; } = string.Empty;
    public string ProductNameAr { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string UnitOfMeasureNameEn { get; set; } = string.Empty;
    public string UnitOfMeasureNameAr { get; set; } = string.Empty;
    public int UnitBaseQuantity { get; set; } = 1;
    public int CurrentQuantity { get; set; }
    public int AdjustmentQuantity { get; set; }
    public int NewQuantity { get; set; }
    public string? Notes { get; set; }
}

public class CreateStockAdjustmentRequest
{
    public Guid WarehouseId { get; set; }
    public string AdjustmentType { get; set; } = string.Empty;
    public string? Explanation { get; set; }
    public List<CreateStockAdjustmentLineRequest> Lines { get; set; } = new();
}

public class CreateStockAdjustmentLineRequest
{
    public Guid UnitId { get; set; }
    public int AdjustmentQuantity { get; set; }
    public string? Notes { get; set; }
}

// ===== Stock Transfer =====

public class StockTransferDto
{
    public Guid Id { get; set; }
    public string TransferNumber { get; set; } = string.Empty;
    public string TransferType { get; set; } = string.Empty; // "ToCentral" or "FromCentral"
    public Guid WarehouseId { get; set; }
    public string WarehouseNameEn { get; set; } = string.Empty;
    public string WarehouseNameAr { get; set; } = string.Empty;
    public Guid FromWarehouseId { get; set; }
    public string FromWarehouseNameEn { get; set; } = string.Empty;
    public string FromWarehouseNameAr { get; set; } = string.Empty;
    public Guid ToWarehouseId { get; set; }
    public string ToWarehouseNameEn { get; set; } = string.Empty;
    public string ToWarehouseNameAr { get; set; } = string.Empty;
    public Guid? RequestedById { get; set; }
    public string? RequestedByName { get; set; }
    public DateTime? RequestedDate { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<StockTransferLineDto> Lines { get; set; } = new();
}

public class StockTransferLineDto
{
    public Guid Id { get; set; }
    public Guid UnitId { get; set; }
    public string UnitBarcode { get; set; } = string.Empty;
    public string ProductNameEn { get; set; } = string.Empty;
    public string ProductNameAr { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string UnitOfMeasureNameEn { get; set; } = string.Empty;
    public string UnitOfMeasureNameAr { get; set; } = string.Empty;
    public int UnitBaseQuantity { get; set; } = 1;
    public int Quantity { get; set; }
    public int SourceQuantityBefore { get; set; }
    public int SourceQuantityAfter { get; set; }
    public int DestinationQuantityBefore { get; set; }
    public int DestinationQuantityAfter { get; set; }
    public string? Notes { get; set; }
}

public class CreateStockTransferRequest
{
    public Guid WarehouseId { get; set; }
    public string TransferType { get; set; } = string.Empty; // "ToCentral" or "FromCentral"
    public string? Notes { get; set; }
    /// <summary>Set when this transfer is created by converting a Purchase Request.</summary>
    public Guid? PurchaseRequestId { get; set; }
    public List<CreateStockTransferLineRequest> Lines { get; set; } = new();
}

public class CreateStockTransferLineRequest
{
    public Guid UnitId { get; set; }
    public int Quantity { get; set; }
    public string? Notes { get; set; }
}

// ===== Inventory History =====

public class InventoryHistoryDto
{
    public Guid Id { get; set; }
    public Guid WarehouseId { get; set; }
    public string WarehouseNameEn { get; set; } = string.Empty;
    public string WarehouseNameAr { get; set; } = string.Empty;
    public Guid ProductId { get; set; }
    public Guid UnitId { get; set; }
    public string UnitOfMeasureNameEn { get; set; } = string.Empty;
    public string UnitOfMeasureNameAr { get; set; } = string.Empty;
    public int UnitBaseQuantity { get; set; } = 1;
    public string UnitBarcode { get; set; } = string.Empty;
    public string ProductNameEn { get; set; } = string.Empty;
    public string ProductNameAr { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public List<string> UnitTypesEn { get; set; } = new();
    public List<string> UnitTypesAr { get; set; } = new();
    public string ActionType { get; set; } = string.Empty;
    public int QuantityChange { get; set; }
    public int AvailableQuantityBefore { get; set; }
    public int AvailableQuantityAfter { get; set; }
    public string ReferenceType { get; set; } = string.Empty;
    public Guid ReferenceId { get; set; }
    public string? PerformedBy { get; set; }
    public DateTime PerformedAt { get; set; }
    public string? Notes { get; set; }
}
