namespace Domain.Enums;

public enum StocktakeLineStatus
{
    /// <summary>Line generated, not yet counted.</summary>
    Pending = 1,

    /// <summary>A counted quantity has been entered.</summary>
    Counted = 2,

    /// <summary>Counted quantity equals system quantity (no difference).</summary>
    Matched = 3,

    /// <summary>Counted quantity differs from system quantity — awaiting review.</summary>
    Flagged = 4,

    /// <summary>Reviewed and approved; any adjustment has been generated.</summary>
    Approved = 5
}
