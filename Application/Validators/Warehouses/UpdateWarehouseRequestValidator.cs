using Application.Features.Warehouses;
using FluentValidation;

namespace Application.Validators.Warehouses;

public class UpdateWarehouseRequestValidator : AbstractValidator<UpdateWarehouseRequest>
{
    public UpdateWarehouseRequestValidator()
    {
        RuleFor(x => x.NameEn)
            .NotEmpty().WithMessage("NameEnRequired")
            .MaximumLength(256).WithMessage("NameEnMaxLength");

        RuleFor(x => x.NameAr)
            .NotEmpty().WithMessage("NameArRequired")
            .MaximumLength(256).WithMessage("NameArMaxLength");

        RuleFor(x => x.WarehouseTypeId)
            .NotEmpty().WithMessage("WarehouseTypeRequired");

        RuleFor(x => x.Address)
            .MaximumLength(500).WithMessage("MaxLengthError")
            .When(x => !string.IsNullOrEmpty(x.Address));

        RuleFor(x => x.ContactPerson)
            .MaximumLength(200).WithMessage("MaxLengthError")
            .When(x => !string.IsNullOrEmpty(x.ContactPerson));

        RuleFor(x => x.ContactPhone)
            .MaximumLength(50).WithMessage("MaxLengthError")
            .When(x => !string.IsNullOrEmpty(x.ContactPhone));

        RuleFor(x => x.Email)
            .MaximumLength(256).WithMessage("MaxLengthError")
            .EmailAddress().WithMessage("InvalidEmail")
            .When(x => !string.IsNullOrEmpty(x.Email));
    }
}
