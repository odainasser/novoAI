using Domain.Common;

namespace Domain.Entities;

public class Terminal : BaseAuditableEntity
{
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;

    /// <summary>
    /// FK to Branch.
    /// </summary>
    public Guid BranchId { get; set; }
    public virtual Branch Branch { get; set; } = null!;

    public string? ComputerIp { get; set; }
    public string? PrinterIp { get; set; }
    public string? PaymentMachineIp { get; set; }

    public bool IsActive { get; set; } = true;
}
