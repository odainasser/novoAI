using Application.Features.Promotions;
using Domain.Enums;
using FluentValidation;

namespace Application.Validators.Promotions;

public class UpdatePromotionRequestValidator : AbstractValidator<UpdatePromotionRequest>
{
    public UpdatePromotionRequestValidator()
    {
        RuleFor(x => x.NameEn)
            .NotEmpty().WithMessage("NameEnRequired")
            .MaximumLength(256).WithMessage("NameEnMaxLength");

        RuleFor(x => x.NameAr)
            .NotEmpty().WithMessage("NameArRequired")
            .MaximumLength(256).WithMessage("NameArMaxLength");

        RuleFor(x => x.DescriptionEn)
            .MaximumLength(1000).WithMessage("DescriptionMaxLength")
            .When(x => !string.IsNullOrEmpty(x.DescriptionEn));

        RuleFor(x => x.DescriptionAr)
            .MaximumLength(1000).WithMessage("DescriptionMaxLength")
            .When(x => !string.IsNullOrEmpty(x.DescriptionAr));

        RuleFor(x => x.DiscountType)
            .IsInEnum().WithMessage("InvalidDiscountType");

        RuleFor(x => x.DiscountValue)
            .GreaterThan(0).WithMessage("DiscountValueMinValue")
            .LessThanOrEqualTo(100).WithMessage("PercentageMaxValue")
            .When(x => x.DiscountType == DiscountType.Percentage);

        RuleFor(x => x.DiscountValue)
            .GreaterThan(0).WithMessage("DiscountValueMinValue")
            .LessThanOrEqualTo(999999999.99m).WithMessage("DiscountValueMaxValue")
            .When(x => x.DiscountType == DiscountType.FixedAmount);

        RuleFor(x => x.ApplyTo)
            .IsInEnum().WithMessage("InvalidApplyToType");

        RuleFor(x => x.StartDateTime)
            .NotEmpty().WithMessage("StartDateRequired")
            .GreaterThan(DateTime.Now).WithMessage("StartDateCannotBeInPast");

        RuleFor(x => x.EndDateTime)
            .NotEmpty().WithMessage("EndDateRequired")
            .GreaterThan(DateTime.Now).WithMessage("EndDateCannotBeInPast")
            .GreaterThan(x => x.StartDateTime).WithMessage("EndDateMustBeAfterStartDate");

        RuleFor(x => x.UnitIds)
            .NotEmpty().WithMessage("SelectAtLeastOneUnit")
            .When(x => x.ApplyTo == PromotionApplyTo.SpecificUnits);

        RuleFor(x => x.CategoryIds)
            .NotEmpty().WithMessage("SelectAtLeastOneCategory")
            .When(x => x.ApplyTo == PromotionApplyTo.Categories);
    }
}
