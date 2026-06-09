using System.Data;
using Application.Common.Interfaces;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Infrastructure.Services;

/// <summary>
/// SQL Server backed implementation of <see cref="INumberSequenceService"/>.
/// Uses a single atomic <c>MERGE … OUTPUT</c> statement (under HOLDLOCK) so the
/// reserve-and-increment happens in one round-trip and concurrent callers are
/// serialized on the target row — no duplicate numbers, no lost updates.
/// </summary>
public class NumberSequenceService : INumberSequenceService
{
    private const string MergeSql = @"
SET NOCOUNT ON;
MERGE dbo.NumberSequences WITH (HOLDLOCK) AS target
USING (SELECT @key AS [Key]) AS source
ON target.[Key] = source.[Key]
WHEN MATCHED THEN
    UPDATE SET [Value] = target.[Value] + 1
WHEN NOT MATCHED THEN
    INSERT ([Key], [Value]) VALUES (@key, 1)
OUTPUT INSERTED.[Value];";

    private readonly ApplicationDbContext _context;

    public NumberSequenceService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<long> NextAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Sequence key is required.", nameof(key));

        // When already inside a caller-managed transaction, run inline so the
        // reservation commits/rolls back atomically with the surrounding work.
        // Otherwise wrap in the configured execution strategy so transient
        // failures retry (EnableRetryOnFailure is on for this context).
        if (_context.Database.CurrentTransaction is not null)
            return await ExecuteAsync(key, cancellationToken);

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(() => ExecuteAsync(key, cancellationToken));
    }

    private async Task<long> ExecuteAsync(string key, CancellationToken cancellationToken)
    {
        var connection = _context.Database.GetDbConnection();
        var openedHere = false;
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
            openedHere = true;
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = MergeSql;

            var current = _context.Database.CurrentTransaction;
            if (current is not null)
                command.Transaction = current.GetDbTransaction();

            var parameter = command.CreateParameter();
            parameter.ParameterName = "@key";
            parameter.Value = key;
            command.Parameters.Add(parameter);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt64(result);
        }
        finally
        {
            if (openedHere)
                await connection.CloseAsync();
        }
    }
}
