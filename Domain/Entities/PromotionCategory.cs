using Domain.Common;

namespace Domain.Entities;

public class PromotionCategory : BaseAuditableEntity
{
    public Guid PromotionId { get; set; }
    public virtual Promotion Promotion { get; set; } = null!;
    
    public Guid CategoryId { get; set; }
    public virtual Category Category { get; set; } = null!;
}
