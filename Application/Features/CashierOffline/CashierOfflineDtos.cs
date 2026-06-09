namespace Application.Features.CashierOffline;

// All data the cashier needs to operate offline for the configured cache window.
// Returned by GET /api/cashier-offline/data in a single round-trip.
public class CashierOfflineDataResponse
{
    public OfflineCredentialDto Credential { get; set; } = new();
    public OfflineProfileDto Profile { get; set; } = new();
    public List<OfflineStoreDto> Stores { get; set; } = new();
    public List<OfflineProductDto> Products { get; set; } = new();
    public List<OfflineShiftDto> Shifts { get; set; } = new();
    public List<OfflineOrderDto> Orders { get; set; } = new();
    public DateTime ServerUtcNow { get; set; }
}

public class OfflineCredentialDto
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
    public List<string> Permissions { get; set; } = new();
    public List<Guid> AssignedStoreIds { get; set; } = new();
    public DateTime IssuedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
}

public class OfflineProfileDto
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public bool CanRefund { get; set; }
}

public class OfflineStoreDto
{
    public Guid StoreId { get; set; }
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? BranchNameEn { get; set; }
    public string? BranchNameAr { get; set; }
    public string? Type { get; set; }
}

public class OfflineProductDto
{
    public Guid ProductId { get; set; }
    public Guid StoreId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? CategoryNameEn { get; set; }
    public string? CategoryNameAr { get; set; }
    public Guid? CategoryId { get; set; }
    public string? ImageUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? ImageETag { get; set; }
    public int AvailableQuantity { get; set; }
    // Carry the source dates so the offline wrapper can mirror the online
    // ProductService ordering (UpdatedAt ?? CreatedAt, descending).
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<OfflineProductUnitDto> Units { get; set; } = new();
}

public class OfflineProductUnitDto
{
    public Guid UnitId { get; set; }
    public string? UnitNameEn { get; set; }
    public string? UnitNameAr { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public decimal SellingPrice { get; set; }
    // Base units per selling unit — the POS divides AvailableQuantity by this
    // to compute how many sellable units remain.
    public int Quantity { get; set; }
    public int LowStockThreshold { get; set; }
    public int AvailableQuantity { get; set; }
    // Preserve so the POS can hide Draft/Inactive units in the unit modal.
    public bool IsActive { get; set; }
}

public class OfflineShiftDto
{
    public Guid ShiftId { get; set; }
    public Guid CashierId { get; set; }
    public Guid? StoreId { get; set; }
    public string? StoreNameEn { get; set; }
    public string? StoreNameAr { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public decimal CashIn { get; set; }
    public decimal CashOut { get; set; }
    public decimal TotalSales { get; set; }
    public decimal TotalReturns { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Comments { get; set; }
}

public class OfflineOrderDto
{
    public Guid OrderId { get; set; }
    public Guid? StoreId { get; set; }
    public string? StoreNameEn { get; set; }
    public string? StoreNameAr { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public decimal? CashAmount { get; set; }
    public decimal? CardAmount { get; set; }
    public decimal Subtotal { get; set; }
    public decimal VatRate { get; set; }
    public decimal VatAmount { get; set; }
    public decimal Total { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CashierId { get; set; }
    public string? CashierName { get; set; }
    public List<OfflineOrderItemDto> Items { get; set; } = new();
    public List<OfflineOrderRefundDto> Refunds { get; set; } = new();
}

public class OfflineOrderItemDto
{
    public Guid OrderItemId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductNameEn { get; set; } = string.Empty;
    public string ProductNameAr { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public Guid? UnitId { get; set; }
    public string? UnitNameEn { get; set; }
    public string? UnitBarcode { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total { get; set; }
}

public class OfflineOrderRefundDto
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Reason { get; set; }
}

