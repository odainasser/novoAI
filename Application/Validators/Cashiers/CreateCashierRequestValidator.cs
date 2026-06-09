using Application.Features.Cashiers;
using FluentValidation;

namespace Application.Validators.Cashiers;

public class CreateCashierRequestValidator : AbstractValidator<CreateCashierRequest>
{
    public CreateCashierRequestValidator()
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
            .Must(PhoneNumberValidation.IsValid).WithMessage("InvalidPhoneNumber")
            .When(x => !string.IsNullOrEmpty(x.PhoneNumber));

        RuleFor(x => x.WarehouseIds)
            .NotEmpty().WithMessage("StoreRequired");
    }
}
