using Application.Features.Branches;
using FluentValidation;

namespace Application.Validators.Branches;

public class UpdateBranchRequestValidator : AbstractValidator<UpdateBranchRequest>
{
    public UpdateBranchRequestValidator()
    {
        RuleFor(x => x.NameEn)
            .NotEmpty().WithMessage("NameEnRequired")
            .MaximumLength(256).WithMessage("NameEnMaxLength");

        RuleFor(x => x.NameAr)
            .NotEmpty().WithMessage("NameArRequired")
            .MaximumLength(256).WithMessage("NameArMaxLength");

        RuleFor(x => x.DescriptionEn)
            .MaximumLength(2000).WithMessage("DescriptionMaxLength")
            .When(x => !string.IsNullOrEmpty(x.DescriptionEn));

        RuleFor(x => x.DescriptionAr)
            .MaximumLength(2000).WithMessage("DescriptionMaxLength")
            .When(x => !string.IsNullOrEmpty(x.DescriptionAr));
    }
}
