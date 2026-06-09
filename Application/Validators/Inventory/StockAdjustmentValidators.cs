using Application.Features.Inventory;
using FluentValidation;

namespace Application.Validators.Inventory;

public class CreateStockAdjustmentRequestValidator : AbstractValidator<CreateStockAdjustmentRequest>
{
    public CreateStockAdjustmentRequestValidator()
    {
        RuleFor(x => x.WarehouseId)
            .NotEmpty().WithMessage("WarehouseRequired");

        RuleFor(x => x.AdjustmentType)
            .NotEmpty().WithMessage("AdjustmentTypeRequired");

        RuleFor(x => x.Lines)
            .NotEmpty().WithMessage("LinesRequired");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.UnitId)
                .NotEmpty().WithMessage("UnitRequired");

            line.RuleFor(l => l.AdjustmentQuantity)
                .GreaterThan(0).WithMessage("AdjustmentQuantityMustBePositive");
        });
    }
}
