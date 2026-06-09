using Application.Features.Auth;
using FluentValidation;

namespace Application.Validators;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("EmailRequired")
            .EmailAddress().WithMessage("InvalidEmailFormat")
            .MaximumLength(256).WithMessage("MaxLengthError");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("PasswordRequired")
            .MinimumLength(8).WithMessage("PasswordTooShort")
            .MaximumLength(128).WithMessage("MaxLengthError")
            .Matches(@"[A-Z]").WithMessage("PasswordRequiresUpper")
            .Matches(@"[a-z]").WithMessage("PasswordRequiresLower")
            .Matches(@"[0-9]").WithMessage("PasswordRequiresDigit")
            .Matches(@"[^a-zA-Z0-9]").WithMessage("PasswordRequiresNonAlphanumeric");

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty().WithMessage("ConfirmPasswordRequired")
            .Equal(x => x.Password).WithMessage("PasswordsDoNotMatch");

        RuleFor(x => x.FirstName)
            .MaximumLength(100).WithMessage("MaxLengthError")
            .Matches(@"^[a-zA-Z\s]*$").WithMessage("InvalidNameFormat")
            .When(x => !string.IsNullOrEmpty(x.FirstName));

        RuleFor(x => x.LastName)
            .MaximumLength(100).WithMessage("MaxLengthError")
            .Matches(@"^[a-zA-Z\s]*$").WithMessage("InvalidNameFormat")
            .When(x => !string.IsNullOrEmpty(x.LastName));
    }
}
