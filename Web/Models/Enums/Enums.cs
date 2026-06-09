namespace Web.Models.Enums;

public enum OrderChannel
{
    POS = 1,
    Online = 2
}

public enum OrderStatus
{
    Completed = 1,
    Refunded = 2,
    PartialRefunded = 3
}

public enum PaymentMethod
{
    Cash = 1,
    Card = 2,
    Mobile = 3,
    Split = 4
}

public enum AuditAction
{
    Created = 1,
    Updated = 2,
    Deleted = 3,
    LoggedIn = 4,
    LoggedOut = 5,
    PasswordChanged = 6,
    PasswordReset = 7,
    EmailVerified = 8,
    TwoFactorEnabled = 9,
    TwoFactorDisabled = 10,
    UpdatedDraft = 11,
    RequestedUpdate = 12,
    SetSellingDetails = 13,
    SetLogisticsDetails = 14,
    RequestedSetSellingDetails = 15,
    RequestedActivation = 16,
    RequestedDeletion = 17,
    RequestedSetLogisticsDetails = 18,
    UpdatedRequest = 19,
    ApprovedRequest = 20,
    RejectedRequest = 21
}

public enum ShiftStatus
{
    Active = 1,
    Completed = 2
}

public enum RequestType
{
    ChangePrice = 1,
    SetUnitPrice = 2,
    ActivateProduct = 3,
    ActivateUnit = 4,
    AddProduct = 5,
    UpdateProduct = 6,
    AddUnit = 7,
    UpdateUnit = 8,
    AddGRN = 9,
    AddStockAdjustment = 10,
    AddStockTransfer = 11,
    DeleteProduct = 12,
    DeleteUnit = 13,
    SetLogisticsDetails = 14,
    AddPurchaseRequest = 15
}

public enum PurchaseRequestStatus
{
    Draft = 1,
    Submitted = 2,
    Approved = 3,
    Rejected = 4,
    Converted = 5,
    Cancelled = 6
}

public enum PurchaseRequestSupplySource
{
    FromSupplier = 1,
    FromCentralWarehouse = 2
}

public enum PurchaseRequestCreationMethod
{
    Manual = 1,
    AutoReorder = 2
}

public enum ConvertedDocumentType
{
    GoodsReceivingNote = 1,
    StockTransfer = 2
}

public enum ItemStatus
{
    Draft = 0,
    Active = 1,
    Inactive = 2,
    Rejected = 3
}

public enum RequestStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3
}

public enum NotificationType
{
    RequestApproved = 1,
    RequestRejected = 2,
    LowStock = 3
}
