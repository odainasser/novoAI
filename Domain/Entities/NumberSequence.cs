namespace Domain.Entities;

/// <summary>
/// Atomic, concurrency-safe document-number counter. One row per logical
/// sequence key (e.g. "ORD-20260529", "GRN-20260529"). The value is reserved
/// via an atomic database UPSERT so two concurrent callers can never receive
/// the same number — replacing the previous "read max + 1" approach that was
/// race-prone under load.
/// </summary>
public class NumberSequence
{
    /// <summary>The logical sequence key (primary key).</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>The most recently issued value for this key.</summary>
    public long Value { get; set; }
}
