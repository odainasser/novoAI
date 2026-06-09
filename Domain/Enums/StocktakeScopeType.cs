namespace Domain.Enums;

public enum StocktakeScopeType
{
    /// <summary>Every unit in the warehouse (used by Full stocktakes).</summary>
    All = 1,

    /// <summary>Every unit whose product belongs to a chosen category (Cycle count).</summary>
    Category = 2,

    /// <summary>An explicit selection of units (Cycle count).</summary>
    Products = 3
}
