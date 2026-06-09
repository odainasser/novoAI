using Application.Features.Roles;
using FluentValidation;

namespace Application.Validators.Roles;

public class UpdateRoleRequestValidator : AbstractValidator<UpdateRoleRequest>
{
    public UpdateRoleRequestValidator()
    {
        RuleFor(x => x.NameEn)
            .NotEmpty().WithMessage("RoleNameRequired")
            .MaximumLength(256).WithMessage("NameEnMaxLength")
            .Matches(@"^[a-zA-Z\s]*$").WithMessage("InvalidNameFormat");

        RuleFor(x => x.NameAr)
            .NotEmpty().WithMessage("RoleNameArRequired")
            .MaximumLength(256).WithMessage("NameArMaxLength");

        RuleFor(x => x.DescriptionEn)
            .MaximumLength(500).WithMessage("DescriptionMaxLength")
            .When(x => !string.IsNullOrEmpty(x.DescriptionEn));

        RuleFor(x => x.DescriptionAr)
            .MaximumLength(500).WithMessage("DescriptionMaxLength")
            .When(x => !string.IsNullOrEmpty(x.DescriptionAr));
    }
}
