using Domain.Common;

namespace Domain.Entities;

public class Lookup : BaseAuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public virtual Lookup? Parent { get; set; }
    public virtual ICollection<Lookup> Children { get; set; } = new List<Lookup>();
    public bool IsActive { get; set; } = true;
}
