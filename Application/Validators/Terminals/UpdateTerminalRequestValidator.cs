using Application.Features.Terminals;
using FluentValidation;

namespace Application.Validators.Terminals;

public class UpdateTerminalRequestValidator : AbstractValidator<UpdateTerminalRequest>
{
    public UpdateTerminalRequestValidator()
    {
        RuleFor(x => x.NameEn)
            .NotEmpty().WithMessage("NameEnRequired")
            .MaximumLength(256).WithMessage("NameEnMaxLength");

        RuleFor(x => x.NameAr)
            .NotEmpty().WithMessage("NameArRequired")
            .MaximumLength(256).WithMessage("NameArMaxLength");

        RuleFor(x => x.BranchId)
            .NotEmpty().WithMessage("BranchRequired");

        RuleFor(x => x.ComputerIp)
            .MaximumLength(45).WithMessage("MaxLengthError")
            .When(x => !string.IsNullOrEmpty(x.ComputerIp));

        RuleFor(x => x.PrinterIp)
            .MaximumLength(45).WithMessage("MaxLengthError")
            .When(x => !string.IsNullOrEmpty(x.PrinterIp));

        RuleFor(x => x.PaymentMachineIp)
            .MaximumLength(45).WithMessage("MaxLengthError")
            .When(x => !string.IsNullOrEmpty(x.PaymentMachineIp));
    }
}
