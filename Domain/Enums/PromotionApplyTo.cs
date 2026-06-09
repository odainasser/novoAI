namespace Domain.Enums;

[Flags]
public enum PromotionApplyTo
{
    None = 0,
    AllSellingUnits = 1,
    SpecificUnits = 2,
    Categories = 4
}
