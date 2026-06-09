using Infrastructure.Persistence;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace UnitTests;

/// <summary>
/// Integration tests for the atomic document-number counter. These need a real
/// SQL Server (the MERGE/OUTPUT is provider-specific), so they only run when the
/// BYTEMART_TEST_CONNECTION environment variable points at a test database with
/// the migrations applied. Without it the tests pass as no-ops so CI stays green.
/// </summary>
public class NumberSequenceServiceTests
{
    private static string? ConnectionString =>
        Environment.GetEnvironmentVariable("BYTEMART_TEST_CONNECTION");

    private static ApplicationDbContext NewContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task NextAsync_returns_sequential_values_for_a_fresh_key()
    {
        var connectionString = ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString)) return; // skipped without a DB

        var key = $"TEST-{Guid.NewGuid():N}";
        await using var context = NewContext(connectionString);
        var service = new NumberSequenceService(context);

        var first = await service.NextAsync(key);
        var second = await service.NextAsync(key);
        var third = await service.NextAsync(key);

        Assert.Equal(1, first);
        Assert.Equal(2, second);
        Assert.Equal(3, third);

        await context.Database.ExecuteSqlRawAsync(
            "DELETE FROM NumberSequences WHERE [Key] = {0}", key);
    }

    [Fact]
    public async Task NextAsync_issues_unique_values_under_concurrency()
    {
        var connectionString = ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString)) return; // skipped without a DB

        var key = $"TEST-{Guid.NewGuid():N}";
        const int concurrency = 50;

        // Each task uses its own DbContext (DbContext is not thread-safe).
        var tasks = Enumerable.Range(0, concurrency).Select(async _ =>
        {
            await using var context = NewContext(connectionString);
            var service = new NumberSequenceService(context);
            return await service.NextAsync(key);
        });

        var results = await Task.WhenAll(tasks);

        // No duplicates, and the full contiguous range was issued.
        Assert.Equal(concurrency, results.Distinct().Count());
        Assert.Equal(Enumerable.Range(1, concurrency), results.OrderBy(v => v).Select(v => (int)v));

        await using var cleanup = NewContext(connectionString);
        await cleanup.Database.ExecuteSqlRawAsync(
            "DELETE FROM NumberSequences WHERE [Key] = {0}", key);
    }
}
