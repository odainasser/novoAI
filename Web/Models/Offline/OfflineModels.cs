using Web.Models.Orders;
using Web.Models.Shifts;

namespace Web.Models.Offline;

// Mirrors Application.Features.CashierOffline.* DTOs. Kept in the Web project so
// the Blazor WASM client does not take a dependency on the server's Application
// assembly. Property names must match the server payload exactly so System.Text.Json
// deserializes them out of the wire format without bespoke converters.

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
    public Guid? CategoryId { get; set; }
    public string? CategoryNameEn { get; set; }
    public string? CategoryNameAr { get; set; }
    public string? ImageUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? ImageETag { get; set; }
    public int AvailableQuantity { get; set; }
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
    public int Quantity { get; set; }
    public int LowStockThreshold { get; set; }
    public int AvailableQuantity { get; set; }
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

// Sync-queue records — local-only. The seq key is auto-assigned by IndexedDB.
public enum SyncQueueOpType
{
    CreateOrder,
    PartialRefund,
    StartShift,
    EndShift
}

public class SyncQueueItem
{
    // IndexedDB auto-assigns this when the record is `append`-ed. Don't set it manually.
    public long? Seq { get; set; }
    public SyncQueueOpType Op { get; set; }
    // JSON-serialized request payload, stored as text for forward-compat.
    public string PayloadJson { get; set; } = string.Empty;
    public Guid? StoreId { get; set; }
    public Guid? TargetId { get; set; }   // e.g. shift id for EndShift / order id for PartialRefund
    public DateTime QueuedAtUtc { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
}

// Payload helpers for the wrapper services — they round-trip through PayloadJson.
public class QueuedCreateOrder
{
    public Guid LocalOrderId { get; set; }
    public Guid? StoreId { get; set; }
    public CreateOrderRequest Request { get; set; } = new();
}

public class QueuedPartialRefund
{
    public Guid OrderId { get; set; }
    public List<PartialRefundItemRequest> Items { get; set; } = new();
    // UTC time the refund was performed offline (captured at enqueue).
    public DateTime ClientCreatedAt { get; set; }
}

public class QueuedStartShift
{
    public Guid LocalShiftId { get; set; }
    public Guid? StoreId { get; set; }
    public StartShiftRequest Request { get; set; } = new();
}

public class QueuedEndShift
{
    public Guid ShiftId { get; set; }
    public EndShiftRequest Request { get; set; } = new();
}
