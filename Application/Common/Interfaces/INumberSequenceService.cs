namespace Application.Common.Interfaces;

/// <summary>
/// Issues gap-tolerant, monotonically increasing numbers for a given sequence
/// key in a concurrency-safe way. Used to generate human-readable document
/// numbers (orders, goods-receiving notes, …) without the race conditions of
/// the legacy "select max and add one" pattern.
/// </summary>
public interface INumberSequenceService
{
    /// <summary>
    /// Atomically reserves and returns the next value for <paramref name="key"/>.
    /// Each caller receives a distinct value even under high concurrency.
    /// </summary>
    Task<long> NextAsync(string key, CancellationToken cancellationToken = default);
}
