using Application.Features.Auth;
using FluentValidation;

namespace Application.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("EmailRequired")
            .EmailAddress().WithMessage("InvalidEmailFormat")
            .MaximumLength(256).WithMessage("MaxLengthError");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("PasswordRequired")
            .MaximumLength(128).WithMessage("MaxLengthError");
    }
}
