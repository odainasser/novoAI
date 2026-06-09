using Web.Models.Enums;

namespace Web.Models.PurchaseRequests;

public class PurchaseRequestDto
{
    public Guid Id { get; set; }
    public string RequestNumber { get; set; } = string.Empty;

    public PurchaseRequestSupplySource SupplySource { get; set; }
    public PurchaseRequestStatus Status { get; set; }
    public PurchaseRequestCreationMethod CreationMethod { get; set; }

    public Guid RequestingWarehouseId { get; set; }
    public string RequestingWarehouseNameEn { get; set; } = string.Empty;
    public string RequestingWarehouseNameAr { get; set; } = string.Empty;
    public Guid? RequestingBranchId { get; set; }

    public Guid? SupplierId { get; set; }
    public string? SupplierNameEn { get; set; }
    public string? SupplierNameAr { get; set; }

    public Guid RequestedById { get; set; }
    public string RequestedByName { get; set; } = string.Empty;

    public DateTime? SubmittedAt { get; set; }

    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public Guid? RejectedById { get; set; }
    public string? RejectedByName { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectReason { get; set; }

    public ConvertedDocumentType? ConvertedDocumentType { get; set; }
    public Guid? ConvertedDocumentId { get; set; }
    public string? ConvertedDocumentReference { get; set; }
    public DateTime? ConvertedAt { get; set; }

    public Guid? LinkedRequestId { get; set; }

    public int TotalItems { get; set; }
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public List<PurchaseRequestLineDto> Lines { get; set; } = new();
}

public class PurchaseRequestLineDto
{
    public Guid Id { get; set; }
    public Guid UnitId { get; set; }
    public string UnitBarcode { get; set; } = string.Empty;
    public Guid ProductId { get; set; }
    public string ProductNameEn { get; set; } = string.Empty;
    public string ProductNameAr { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string UnitOfMeasureNameEn { get; set; } = string.Empty;
    public string UnitOfMeasureNameAr { get; set; } = string.Empty;
    public int UnitBaseQuantity { get; set; } = 1;
    public int RequestedQuantity { get; set; }
    public int? SuggestedQuantity { get; set; }
    public int CurrentAvailableQuantity { get; set; }
    public string? Notes { get; set; }
}

public class CreatePurchaseRequestRequest
{
    public PurchaseRequestSupplySource SupplySource { get; set; } = PurchaseRequestSupplySource.FromCentralWarehouse;
    public Guid RequestingWarehouseId { get; set; }
    public Guid? SupplierId { get; set; }
    public string? Notes { get; set; }
    public List<CreatePurchaseRequestLineRequest> Lines { get; set; } = new();
}

public class CreatePurchaseRequestLineRequest
{
    public Guid UnitId { get; set; }
    public int RequestedQuantity { get; set; }
    public string? Notes { get; set; }
}

public class UpdatePurchaseRequestRequest
{
    public PurchaseRequestSupplySource SupplySource { get; set; }
    public Guid? SupplierId { get; set; }
    public string? Notes { get; set; }
    public List<CreatePurchaseRequestLineRequest> Lines { get; set; } = new();
}

public class ReviewPurchaseRequestRequest
{
    public string? Note { get; set; }
}
