using Application.Features.Lookups;
using FluentValidation;

namespace Application.Validators.Lookups;

public class CreateLookupRequestValidator : AbstractValidator<CreateLookupRequest>
{
    public CreateLookupRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("CodeRequired")
            .MaximumLength(100).WithMessage("MaxLengthError")
            .Matches(@"^[a-zA-Z0-9_-]+$").WithMessage("CodeInvalidFormat");

        RuleFor(x => x.NameEn)
            .NotEmpty().WithMessage("NameEnRequired")
            .MaximumLength(256).WithMessage("NameEnMaxLength");

        RuleFor(x => x.NameAr)
            .NotEmpty().WithMessage("NameArRequired")
            .MaximumLength(256).WithMessage("NameArMaxLength");
    }
}
