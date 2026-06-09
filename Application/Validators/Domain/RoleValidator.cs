using Domain.Entities;
using FluentValidation;

namespace Application.Validators.Domain;

public class RoleValidator : AbstractValidator<Role>
{
    public RoleValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Role name is required.")
            .MaximumLength(256).WithMessage("Role name must not exceed 256 characters.")
            .Matches(@"^[a-zA-Z0-9\s_-]+$").WithMessage("Role name can only contain letters, numbers, spaces, underscores, and hyphens.");

        RuleFor(x => x.DescriptionEn)
            .MaximumLength(500).WithMessage("English Description must not exceed 500 characters.")
            .When(x => !string.IsNullOrEmpty(x.DescriptionEn));

        RuleFor(x => x.DescriptionAr)
            .MaximumLength(500).WithMessage("Arabic Description must not exceed 500 characters.")
            .When(x => !string.IsNullOrEmpty(x.DescriptionAr));
    }
}
