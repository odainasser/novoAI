using Application.Features.Users;
using FluentValidation;

namespace Application.Validators.Users;

public class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("EmailRequired")
            .EmailAddress().WithMessage("InvalidEmailFormat")
            .MaximumLength(256).WithMessage("MaxLengthError");

        RuleFor(x => x.FirstName)
            .MaximumLength(100).WithMessage("MaxLengthError")
            .Matches(@"^[a-zA-Z\s]*$").WithMessage("InvalidNameFormat")
            .When(x => !string.IsNullOrEmpty(x.FirstName));

        RuleFor(x => x.LastName)
            .MaximumLength(100).WithMessage("MaxLengthError")
            .Matches(@"^[a-zA-Z\s]*$").WithMessage("InvalidNameFormat")
            .When(x => !string.IsNullOrEmpty(x.LastName));

        RuleFor(x => x.PhoneNumber)
            .Matches(@"^\+?[1-9]\d{1,14}$").WithMessage("InvalidPhoneNumber")
            .When(x => !string.IsNullOrEmpty(x.PhoneNumber));
    }
}
