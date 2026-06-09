using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class Promotion : BaseAuditableEntity
{
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? DescriptionEn { get; set; }
    public string? DescriptionAr { get; set; }
    public DiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public PromotionApplyTo ApplyTo { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public bool IsActive { get; set; } = true;
    
    // Navigation properties for specific units and categories
    public virtual ICollection<PromotionUnit> PromotionUnits { get; set; } = new List<PromotionUnit>();
    public virtual ICollection<PromotionCategory> PromotionCategories { get; set; } = new List<PromotionCategory>();
}
