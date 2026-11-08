using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Discounts.Reports;
using Sugarista.Infrastructure.Data.DbConnection;

namespace Sugarista.Infrastructure.Services;

/// <summary>
/// Checks scheduled reports every minute and sends the ones that are due. All
/// schedule fields (frequency, hour, recipients, columns, filters) are read
/// from the database on every tick, so admin edits apply without a restart.
/// </summary>
public class ReportScheduleRunnerService : BackgroundService
{
    private static readonly TimeSpan TickPeriod = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(20);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DbConnectionManager _connectionManager;
    private readonly ILogger<ReportScheduleRunnerService> _logger;

    public ReportScheduleRunnerService(
        IServiceScopeFactory scopeFactory,
        DbConnectionManager connectionManager,
        ILogger<ReportScheduleRunnerService> logger)
    {
        _scopeFactory = scopeFactory;
        _connectionManager = connectionManager;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await BackgroundDatabaseGate.WaitUntilConfiguredAsync(_connectionManager, _logger, nameof(ReportScheduleRunnerService), stoppingToken);

        await WaitForDatabaseAsync(stoppingToken);

        using var timer = new PeriodicTimer(TickPeriod);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var dispatcher = scope.ServiceProvider.GetRequiredService<ReportScheduleDispatcher>();
                    await dispatcher.DispatchDueAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Scheduled report tick failed.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Host shutting down — expected on graceful stop.
        }
    }

    private async Task WaitForDatabaseAsync(CancellationToken stoppingToken)
    {
        var delay = StartupDelay;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
                await dbContext.ReportSchedules.AsNoTracking().AnyAsync(stoppingToken);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Report schedule runner could not reach the database yet. Retrying in {Seconds}s.",
                    delay.TotalSeconds);

                await Task.Delay(delay, stoppingToken);
                delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 600));
                continue;
            }

            await Task.Delay(StartupDelay, stoppingToken);
            return;
        }
    }
}
