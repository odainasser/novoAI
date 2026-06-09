using Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

/// <summary>
/// Periodically scans stock balances and creates draft auto-reorder Purchase Request proposals
/// for units at or below their reorder point. Proposals are ALWAYS drafts — a human reviews
/// and submits them; nothing is auto-submitted and no stock is moved.
/// </summary>
public class AutoReorderHostedService : BackgroundService
{
    // A simple hosted service (the solution has no job scheduler). Scans once per day.
    private static readonly TimeSpan ScanInterval = TimeSpan.FromHours(24);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(2);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AutoReorderHostedService> _logger;

    public AutoReorderHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<AutoReorderHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Give the host time to apply migrations and seed before the first scan.
        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(ScanInterval);
        do
        {
            await RunScanAsync(stoppingToken);
        }
        while (await SafeWaitAsync(timer, stoppingToken));
    }

    private async Task RunScanAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IPurchaseRequestService>();
            var created = await service.GenerateAutoReorderProposalsAsync(cancellationToken: stoppingToken);
            if (created > 0)
                _logger.LogInformation("Auto-reorder scan created {Count} draft purchase request proposal(s).", created);
        }
        catch (OperationCanceledException)
        {
            // Shutting down — ignore.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auto-reorder scan failed.");
        }
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken token)
    {
        try
        {
            return await timer.WaitForNextTickAsync(token);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
