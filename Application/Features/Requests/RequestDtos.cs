using Domain.Enums;

namespace Application.Features.Requests;

public class RequestDto
{
    public Guid Id { get; set; }
    public RequestType Type { get; set; }
    public RequestStatus Status { get; set; }

    public Guid RequestedById { get; set; }
    public string RequestedByName { get; set; } = string.Empty;

    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public Guid? RejectedById { get; set; }
    public string? RejectedByName { get; set; }
    public DateTime? RejectedAt { get; set; }

    public string? ReviewNote { get; set; }

    // ChangePrice
    public Guid? ProductId { get; set; }
    public string? ProductName { get; set; }
    public decimal? CurrentPrice { get; set; }
    public decimal? NewPrice { get; set; }

    // SetUnitPrice
    public Guid? UnitId { get; set; }

    public string? Note { get; set; }

    public string? NewDataJson { get; set; }
    public string? OldDataJson { get; set; }

    public DateTime CreatedAt { get; set; }
}


public class CreateSetUnitPriceRequest
{
    public Guid UnitId { get; set; }
    public decimal NewPrice { get; set; }
    public string? SellingBarcode { get; set; }
    public int LowStockThreshold { get; set; } = 10;
    public string? Note { get; set; }
}

public class CreateActivateProductRequest
{
    public Guid ProductId { get; set; }
    public string? Note { get; set; }
}

public class CreateActivateUnitRequest
{
    public Guid UnitId { get; set; }
    public string? Note { get; set; }
}

public class ReviewRequestDto
{
    public bool Approve { get; set; }
    public string? ReviewNote { get; set; }
}

public class CreateInventoryGRNRequest
{
    public Application.Features.Inventory.CreateGoodsReceivingNoteRequest Data { get; set; } = new();
    public string? Note { get; set; }
}

public class CreateInventoryAdjustmentRequest
{
    public Application.Features.Inventory.CreateStockAdjustmentRequest Data { get; set; } = new();
    public string? Note { get; set; }
}

public class CreateInventoryTransferRequest
{
    public Application.Features.Inventory.CreateStockTransferRequest Data { get; set; } = new();
    public string? Note { get; set; }
}

public class CreateDeleteProductRequest
{
    public Guid ProductId { get; set; }
    public string? Note { get; set; }
}

public class CreateDeleteUnitRequest
{
    public Guid UnitId { get; set; }
    public string? Note { get; set; }
}

public class CreateSetLogisticsDetailsRequest
{
    public Guid UnitId { get; set; }
    public decimal NewCost { get; set; }
    public List<Application.Features.Units.UnitSupplierItem> Suppliers { get; set; } = new();
    public int LowStockThreshold { get; set; } = 10;
    public string? Note { get; set; }
}
