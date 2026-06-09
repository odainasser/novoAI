namespace Web.Models.Inventory;

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
    public string TransferType { get; set; } = string.Empty;
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
    public string TransferType { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public List<CreateStockTransferLineRequest> Lines { get; set; } = new();
}

public class CreateStockTransferLineRequest
{
    public Guid UnitId { get; set; }
    public int Quantity { get; set; }
    public string? Notes { get; set; }
}

// ===== Stocktake (physical count / cycle count) =====

public class StocktakeDto
{
    public Guid Id { get; set; }
    public string StocktakeNumber { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseNameEn { get; set; } = string.Empty;
    public string WarehouseNameAr { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;        // Full | Cycle
    public string ScopeType { get; set; } = string.Empty;   // All | Category | Products
    public Guid? ScopeCategoryId { get; set; }
    public string? ScopeCategoryNameEn { get; set; }
    public string? ScopeCategoryNameAr { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? CreatedById { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? Notes { get; set; }
    public int TotalLines { get; set; }
    public int CountedLines { get; set; }
    public int MatchedLines { get; set; }
    public int FlaggedLines { get; set; }
    public List<StocktakeLineDto> Lines { get; set; } = new();
}

public class StocktakeLineDto
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
    public int SystemQuantity { get; set; }
    public int? CountedQuantity { get; set; }
    public int Difference { get; set; }
    public string LineStatus { get; set; } = string.Empty;
    public string? AdjustmentType { get; set; }
    public Guid? GeneratedAdjustmentId { get; set; }
    public string? GeneratedAdjustmentNumber { get; set; }
    public string? Notes { get; set; }
}

public class CreateStocktakeRequest
{
    public Guid WarehouseId { get; set; }
    public string Type { get; set; } = string.Empty;        // Full | Cycle
    public string ScopeType { get; set; } = "All";          // All | Category | Products
    public Guid? ScopeCategoryId { get; set; }
    public List<Guid>? UnitIds { get; set; }
    public string? Notes { get; set; }
}

public class SaveStocktakeCountsRequest
{
    public List<StocktakeCountLineRequest> Lines { get; set; } = new();
}

public class StocktakeCountLineRequest
{
    public Guid LineId { get; set; }
    public int CountedQuantity { get; set; }
    public string? Notes { get; set; }
}

public class ApproveStocktakeRequest
{
    public List<ApproveStocktakeLineRequest> Lines { get; set; } = new();
}

public class ApproveStocktakeLineRequest
{
    public Guid LineId { get; set; }
    public string? AdjustmentType { get; set; }   // Loss | Theft | Damage | Expiry | CorrectionAdd
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
