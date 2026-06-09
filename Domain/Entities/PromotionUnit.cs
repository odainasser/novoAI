using Domain.Common;

namespace Domain.Entities;

public class PromotionUnit : BaseAuditableEntity
{
    public Guid PromotionId { get; set; }
    public virtual Promotion Promotion { get; set; } = null!;
    
    public Guid UnitId { get; set; }
    public virtual Unit Unit { get; set; } = null!;
}
