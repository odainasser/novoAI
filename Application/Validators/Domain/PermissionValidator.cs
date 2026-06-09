using Domain.Constants;
using Domain.Entities;
using FluentValidation;

namespace Application.Validators.Domain;

public class PermissionValidator : AbstractValidator<Permission>
{
    public PermissionValidator()
    {
        RuleFor(x => x.NameEn)
            .NotEmpty().WithMessage("Permission English name is required.")
            .MaximumLength(256).WithMessage("Permission English name must not exceed 256 characters.")
            .Matches(@"^[a-zA-Z0-9\s_-]+$").WithMessage("Permission English name can only contain letters, numbers, spaces, underscores, and hyphens.");

        RuleFor(x => x.NameAr)
            .NotEmpty().WithMessage("Permission Arabic name is required.")
            .MaximumLength(256).WithMessage("Permission Arabic name must not exceed 256 characters.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Permission code is required.")
            .MaximumLength(100).WithMessage("Permission code must not exceed 100 characters.")
            .Matches(@"^[a-z]+\.[a-z]+$").WithMessage("Permission code must follow the format 'module.action' (e.g., 'users.read').")
            .Must(BeValidPermissionCode).WithMessage("Permission code must be one of the predefined permissions.");

        RuleFor(x => x.DescriptionEn)
            .MaximumLength(500).WithMessage("English description must not exceed 500 characters.")
            .When(x => !string.IsNullOrEmpty(x.DescriptionEn));

        RuleFor(x => x.DescriptionAr)
            .MaximumLength(500).WithMessage("Arabic description must not exceed 500 characters.")
            .When(x => !string.IsNullOrEmpty(x.DescriptionAr));

        RuleFor(x => x.Module)
            .MaximumLength(100).WithMessage("Module name must not exceed 100 characters.")
            .Matches(@"^[a-zA-Z0-9_-]+$").WithMessage("Module name can only contain letters, numbers, underscores, and hyphens.")
            .When(x => !string.IsNullOrEmpty(x.Module));
    }

    private bool BeValidPermissionCode(string code)
    {
        return Permissions.IsValid(code);
    }
}
