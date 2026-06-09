using Application.Features.Products;
using FluentValidation;

namespace Application.Validators.Products;

public class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
{
    public UpdateProductRequestValidator()
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

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("CodeRequired")
            .MaximumLength(100).WithMessage("CodeMaxLength")
            .Matches(@"^[a-zA-Z0-9\-_]+$").WithMessage("CodeInvalidFormat");
    }
}
