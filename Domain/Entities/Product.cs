using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class Product : BaseAuditableEntity
{
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? DescriptionEn { get; set; }
    public string? DescriptionAr { get; set; }
    public string Code { get; set; } = string.Empty;
    public Guid? CategoryId { get; set; }
    public virtual Category? Category { get; set; }
    public ItemStatus Status { get; set; } = ItemStatus.Draft;
    public bool IsActive { get; set; } = false;
}
