using Application.Features.Inventory;
using FluentValidation;

namespace Application.Validators.Inventory;

public class CreateStockTransferRequestValidator : AbstractValidator<CreateStockTransferRequest>
{
    public CreateStockTransferRequestValidator()
    {
        RuleFor(x => x.WarehouseId)
            .NotEmpty().WithMessage("WarehouseRequired");

        RuleFor(x => x.TransferType)
            .NotEmpty().WithMessage("TransferTypeRequired")
            .Must(x => x is "ToCentral" or "FromCentral").WithMessage("InvalidTransferType");

        RuleFor(x => x.Lines)
            .NotEmpty().WithMessage("LinesRequired");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.UnitId)
                .NotEmpty().WithMessage("UnitRequired");

            line.RuleFor(l => l.Quantity)
                .GreaterThan(0).WithMessage("QuantityMustBePositive");
        });
    }
}
