using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class Request : BaseAuditableEntity
{
    public RequestType Type { get; set; }
    public RequestStatus Status { get; set; } = RequestStatus.Pending;

    // Who submitted the request
    public Guid RequestedById { get; set; }
    public string RequestedByName { get; set; } = string.Empty;

    // Who approved the request
    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }

    // Who rejected the request
    public Guid? RejectedById { get; set; }
    public string? RejectedByName { get; set; }
    public DateTime? RejectedAt { get; set; }

    // Reviewer note (set on approval or rejection)
    public string? ReviewNote { get; set; }

    // ChangePrice fields
    public Guid? ProductId { get; set; }
    public string? ProductName { get; set; }
    public decimal? CurrentPrice { get; set; }
    public decimal? NewPrice { get; set; }

    // SetUnitPrice fields
    public Guid? UnitId { get; set; }

    // JSON payload for add / update requests
    public string? NewDataJson { get; set; }
    public string? OldDataJson { get; set; }

    // Optional note from requester
    public string? Note { get; set; }
}
