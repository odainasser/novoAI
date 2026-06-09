using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class PurchaseRequest : BaseAuditableEntity
{
    public string RequestNumber { get; set; } = string.Empty;

    public PurchaseRequestSupplySource SupplySource { get; set; }

    /// <summary>The branch warehouse that needs stock.</summary>
    public Guid RequestingWarehouseId { get; set; }
    public virtual Warehouse RequestingWarehouse { get; set; } = null!;

    /// <summary>Optional — only set when <see cref="SupplySource"/> is FromSupplier.</summary>
    public Guid? SupplierId { get; set; }
    public virtual Supplier? Supplier { get; set; }

    public PurchaseRequestStatus Status { get; set; } = PurchaseRequestStatus.Draft;

    public PurchaseRequestCreationMethod CreationMethod { get; set; } = PurchaseRequestCreationMethod.Manual;

    // Requester (snapshot pair, matching GRN/Transfer/Request convention)
    public Guid RequestedById { get; set; }
    public string RequestedByName { get; set; } = string.Empty;

    public DateTime? SubmittedAt { get; set; }

    // Approval
    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }

    // Rejection
    public Guid? RejectedById { get; set; }
    public string? RejectedByName { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectReason { get; set; }

    // Conversion result
    public ConvertedDocumentType? ConvertedDocumentType { get; set; }
    public Guid? ConvertedDocumentId { get; set; }
    public string? ConvertedDocumentReference { get; set; }
    public DateTime? ConvertedAt { get; set; }

    /// <summary>Links to the mirror <see cref="Request"/> row that surfaces this PR in the unified approvals inbox.</summary>
    public Guid? LinkedRequestId { get; set; }

    public int TotalItems { get; set; }

    public string? Notes { get; set; }

    public virtual ICollection<PurchaseRequestLine> Lines { get; set; } = new List<PurchaseRequestLine>();
}
