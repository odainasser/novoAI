using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

/// <summary>
/// Shared transactional-execution helpers for stock-mutating services.
/// Centralizes the EF execution-strategy + transaction wrapping and the bounded
/// optimistic-concurrency retry so every path that touches StockBalance behaves
/// identically (orders, goods-receiving, adjustments, transfers).
/// </summary>
public static class ConcurrencyRetry
{
    /// <summary>
    /// Runs <paramref name="work"/> inside the configured execution strategy and a
    /// database transaction. If a caller-managed transaction is already active the
    /// work runs inline without nesting.
    /// </summary>
    public static async Task ExecuteInTransactionAsync(ApplicationDbContext context, Func<Task> work)
    {
        if (context.Database.CurrentTransaction != null)
        {
            await work();
            return;
        }

        var strategy = context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await context.Database.BeginTransactionAsync();
            await work();
            await tx.CommitAsync();
        });
    }

    /// <summary>
    /// Runs a transactional unit of work and, on optimistic-concurrency conflict,
    /// clears the change tracker and retries with a short backoff. Only safe for
    /// units of work that build/load all their state internally (each retry re-runs
    /// the closure from scratch) — callers that mutate pre-loaded tracked entities
    /// must use <see cref="ExecuteInTransactionAsync"/> and handle the conflict
    /// themselves.
    /// </summary>
    public static async Task ExecuteWithRetryAsync(
        ApplicationDbContext context,
        Func<Task> work,
        string conflictMessage,
        int maxAttempts = 3)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await ExecuteInTransactionAsync(context, work);
                return;
            }
            catch (DbUpdateConcurrencyException)
            {
                // Detach everything so the next attempt re-reads fresh stock balances.
                context.ChangeTracker.Clear();
                if (attempt >= maxAttempts)
                    throw new InvalidOperationException(conflictMessage);
                await Task.Delay(40 * attempt);
            }
        }
    }
}
