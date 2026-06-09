using Domain.Common;

namespace Domain.Entities;

public class Supplier : BaseAuditableEntity
{
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? ContactPersonEn { get; set; }
    public string? ContactPersonAr { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public bool IsActive { get; set; } = true;
}
