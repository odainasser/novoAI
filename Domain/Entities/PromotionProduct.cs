using Domain.Common;

namespace Domain.Entities;

public class PromotionProduct : BaseAuditableEntity
{
    public Guid PromotionId { get; set; }
    public virtual Promotion Promotion { get; set; } = null!;
    
    public Guid ProductId { get; set; }
    public virtual Product Product { get; set; } = null!;
}
