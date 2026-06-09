using Application.Features.Inventory;
using FluentValidation;

namespace Application.Validators.Inventory;

public class CreateGoodsReceivingNoteRequestValidator : AbstractValidator<CreateGoodsReceivingNoteRequest>
{
    public CreateGoodsReceivingNoteRequestValidator()
    {
        RuleFor(x => x.Lines)
            .NotEmpty().WithMessage("LinesRequired");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.UnitId)
                .NotEmpty().WithMessage("UnitRequired");

            line.RuleFor(l => l.SupplierId)
                .NotEmpty().WithMessage("SupplierRequired");

            line.RuleFor(l => l.ReceivedQuantity)
                .GreaterThan(0).WithMessage("ReceivedQuantityMustBePositive");

            line.RuleFor(l => l.Cost)
                .GreaterThanOrEqualTo(0).WithMessage("CostMustBeNonNegative");
        });
    }
}
