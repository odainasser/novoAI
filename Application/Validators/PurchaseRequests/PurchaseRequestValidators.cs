using Application.Features.PurchaseRequests;
using Domain.Enums;
using FluentValidation;

namespace Application.Validators.PurchaseRequests;

public class CreatePurchaseRequestRequestValidator : AbstractValidator<CreatePurchaseRequestRequest>
{
    public CreatePurchaseRequestRequestValidator()
    {
        RuleFor(x => x.RequestingWarehouseId)
            .NotEmpty().WithMessage("WarehouseRequired");

        RuleFor(x => x.SupplierId)
            .NotEmpty()
            .When(x => x.SupplySource == PurchaseRequestSupplySource.FromSupplier)
            .WithMessage("SupplierRequiredForSupplierSource");

        RuleFor(x => x.Lines)
            .NotEmpty().WithMessage("LinesRequired");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.UnitId)
                .NotEmpty().WithMessage("UnitRequired");

            line.RuleFor(l => l.RequestedQuantity)
                .GreaterThan(0).WithMessage("QuantityMustBePositive");
        });
    }
}

public class UpdatePurchaseRequestRequestValidator : AbstractValidator<UpdatePurchaseRequestRequest>
{
    public UpdatePurchaseRequestRequestValidator()
    {
        RuleFor(x => x.SupplierId)
            .NotEmpty()
            .When(x => x.SupplySource == PurchaseRequestSupplySource.FromSupplier)
            .WithMessage("SupplierRequiredForSupplierSource");

        RuleFor(x => x.Lines)
            .NotEmpty().WithMessage("LinesRequired");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.UnitId)
                .NotEmpty().WithMessage("UnitRequired");

            line.RuleFor(l => l.RequestedQuantity)
                .GreaterThan(0).WithMessage("QuantityMustBePositive");
        });
    }
}
