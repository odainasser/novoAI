using Application.Features.Units;
using FluentValidation;

namespace Application.Validators.Units;

public class UpdateUnitRequestValidator : AbstractValidator<UpdateUnitRequest>
{
    public UpdateUnitRequestValidator()
    {
        RuleFor(x => x.UnitOfMeasureId)
            .NotEmpty().WithMessage("UnitOfMeasureRequired");

        RuleFor(x => x.UnitTypeIds)
            .NotEmpty().WithMessage("UnitTypeRequired");

        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("ProductRequired");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("BaseUnitsMustBePositive");

        RuleFor(x => x.LowStockThreshold)
            .GreaterThanOrEqualTo(0).WithMessage("LowStockThresholdMinValue");
    }
}
