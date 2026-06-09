using Application.Features.Auth;
using FluentValidation;

namespace Application.Validators;

public class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("EmailRequired")
            .EmailAddress().WithMessage("InvalidEmailFormat")
            .MaximumLength(256).WithMessage("MaxLengthError");

        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("TokenRequired")
            .MaximumLength(2048).WithMessage("MaxLengthError");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("NewPasswordRequired")
            .MinimumLength(8).WithMessage("PasswordTooShort")
            .MaximumLength(128).WithMessage("MaxLengthError")
            .Matches(@"[A-Z]").WithMessage("PasswordRequiresUpper")
            .Matches(@"[a-z]").WithMessage("PasswordRequiresLower")
            .Matches(@"[0-9]").WithMessage("PasswordRequiresDigit")
            .Matches(@"[^a-zA-Z0-9]").WithMessage("PasswordRequiresNonAlphanumeric");

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty().WithMessage("ConfirmPasswordRequired")
            .Equal(x => x.NewPassword).WithMessage("PasswordsDoNotMatch");
    }
}
