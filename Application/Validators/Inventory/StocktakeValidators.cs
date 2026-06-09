using Application.Features.Inventory;
using FluentValidation;

namespace Application.Validators.Inventory;

public class CreateStocktakeRequestValidator : AbstractValidator<CreateStocktakeRequest>
{
    private static readonly string[] Types = { "Full", "Cycle" };
    private static readonly string[] Scopes = { "All", "Category", "Products" };

    public CreateStocktakeRequestValidator()
    {
        RuleFor(x => x.WarehouseId)
            .NotEmpty().WithMessage("WarehouseRequired");

        RuleFor(x => x.Type)
            .NotEmpty().WithMessage("StocktakeTypeRequired")
            .Must(t => Types.Contains(t, StringComparer.OrdinalIgnoreCase))
            .WithMessage("StocktakeTypeInvalid");

        RuleFor(x => x.ScopeType)
            .NotEmpty().WithMessage("StocktakeScopeRequired")
            .Must(s => Scopes.Contains(s, StringComparer.OrdinalIgnoreCase))
            .WithMessage("StocktakeScopeInvalid");

        // Full stocktakes always count everything.
        RuleFor(x => x.ScopeType)
            .Equal("All", StringComparer.OrdinalIgnoreCase)
            .When(x => string.Equals(x.Type, "Full", StringComparison.OrdinalIgnoreCase))
            .WithMessage("FullStocktakeMustCountAll");

        // Cycle counts must define a scope.
        RuleFor(x => x.ScopeCategoryId)
            .NotNull().WithMessage("StocktakeCategoryRequired")
            .When(x => string.Equals(x.ScopeType, "Category", StringComparison.OrdinalIgnoreCase));

        RuleFor(x => x.UnitIds)
            .NotEmpty().WithMessage("StocktakeProductsRequired")
            .When(x => string.Equals(x.ScopeType, "Products", StringComparison.OrdinalIgnoreCase));
    }
}

public class SaveStocktakeCountsRequestValidator : AbstractValidator<SaveStocktakeCountsRequest>
{
    public SaveStocktakeCountsRequestValidator()
    {
        RuleFor(x => x.Lines)
            .NotEmpty().WithMessage("LinesRequired");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.LineId)
                .NotEmpty().WithMessage("LineRequired");

            line.RuleFor(l => l.CountedQuantity)
                .GreaterThanOrEqualTo(0).WithMessage("CountedQuantityMustBeNonNegative");
        });
    }
}

public class ApproveStocktakeRequestValidator : AbstractValidator<ApproveStocktakeRequest>
{
    public ApproveStocktakeRequestValidator()
    {
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.LineId)
                .NotEmpty().WithMessage("LineRequired");
        });
    }
}
