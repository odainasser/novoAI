namespace Domain.Enums;

public enum StocktakeType
{
    /// <summary>Counts every unit held in the warehouse in one session.</summary>
    Full = 1,

    /// <summary>Counts a subset of units (a category or a product selection) on a rotating basis.</summary>
    Cycle = 2
}
