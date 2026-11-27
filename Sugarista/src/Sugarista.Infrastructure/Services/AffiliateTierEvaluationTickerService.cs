using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sugarista.Application.Affiliates.Services;
using Sugarista.Infrastructure.Data.DbConnection;

namespace Sugarista.Infrastructure.Services;

/// <summary>
/// Periodic background worker that evaluates all active affiliates' rolling
/// 30-day approved sales and automatically upgrades or downgrades their tiers.
/// </summary>
public sealed class AffiliateTierEvaluationTickerService : BackgroundService
{
    private static readonly TimeSpan TickPeriod = TimeSpan.FromHours(6);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(45);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DbConnectionManager _connectionManager;
    private readonly ILogger<AffiliateTierEvaluationTickerService> _logger;

    public AffiliateTierEvaluationTickerService(
        IServiceScopeFactory scopeFactory,
        DbConnectionManager connectionManager,
        ILogger<AffiliateTierEvaluationTickerService> logger)
    {
        _scopeFactory = scopeFactory;
        _connectionManager = connectionManager;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await BackgroundDatabaseGate.WaitUntilConfiguredAsync(
            _connectionManager,
            _logger,
            nameof(AffiliateTierEvaluationTickerService),
            stoppingToken);

        await Task.Delay(StartupDelay, stoppingToken);

        using var timer = new PeriodicTimer(TickPeriod);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var engine = scope.ServiceProvider.GetRequiredService<IAffiliateTierEngine>();
                    var result = await engine.EvaluateAllActiveAffiliatesAsync(cancellationToken: stoppingToken);

                    if (result.UpgradedCount > 0 || result.DowngradedCount > 0)
                    {
                        _logger.LogInformation(
                            "Affiliate tier sweep completed: {Evaluated} evaluated, {Upgraded} upgraded, {Downgraded} downgraded.",
                            result.EvaluatedCount,
                            result.UpgradedCount,
                            result.DowngradedCount);
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Affiliate tier evaluation tick failed.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on graceful shutdown.
        }
    }
}
