using Application.Features.Categories;
using FluentValidation;

namespace Application.Validators.Categories;

public class UpdateCategoryRequestValidator : AbstractValidator<UpdateCategoryRequest>
{
    public UpdateCategoryRequestValidator()
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

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0).WithMessage("SortOrderMinValue")
            .LessThanOrEqualTo(10000).WithMessage("SortOrderMaxValue");
    }
}
