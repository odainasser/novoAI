using Application.Features.Suppliers;
using FluentValidation;

namespace Application.Validators.Suppliers;

public class CreateSupplierRequestValidator : AbstractValidator<CreateSupplierRequest>
{
    public CreateSupplierRequestValidator()
    {
        RuleFor(x => x.NameEn)
            .NotEmpty().WithMessage("NameEnRequired")
            .MaximumLength(200).WithMessage("NameEnMaxLength");

        RuleFor(x => x.NameAr)
            .NotEmpty().WithMessage("NameArRequired")
            .MaximumLength(200).WithMessage("NameArMaxLength");

        RuleFor(x => x.ContactPersonEn)
            .MaximumLength(200).WithMessage("MaxLengthError")
            .Matches(@"^[a-zA-Z\s'-]*$").WithMessage("InvalidNameFormat")
            .When(x => !string.IsNullOrEmpty(x.ContactPersonEn));

        RuleFor(x => x.ContactPersonAr)
            .MaximumLength(200).WithMessage("MaxLengthError")
            .When(x => !string.IsNullOrEmpty(x.ContactPersonAr));

        RuleFor(x => x.ContactEmail)
            .EmailAddress().WithMessage("InvalidEmailFormat")
            .MaximumLength(256).WithMessage("MaxLengthError")
            .When(x => !string.IsNullOrEmpty(x.ContactEmail));

        RuleFor(x => x.ContactPhone)
            .Matches(@"^\+?[1-9]\d{1,14}$").WithMessage("InvalidPhoneNumber")
            .When(x => !string.IsNullOrEmpty(x.ContactPhone));
    }
}
