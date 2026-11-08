using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Maintenance;
using Sugarista.Infrastructure.Data.DbConnection;

namespace Sugarista.Infrastructure.Services;

/// <summary>
/// Hourly background ticker that walks the maintenance schedules table and
/// fires SMS/email reminders for every due cycle (Tehran calendar). A due
/// schedule fires once, then rolls forward a full interval — so each period
/// reminds exactly one time even if the process was down for a while.
/// </summary>
public class MaintenanceReminderTickerService : BackgroundService
{
    private static readonly TimeSpan TickPeriod = TimeSpan.FromHours(1);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(20);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DbConnectionManager _connectionManager;
    private readonly ILogger<MaintenanceReminderTickerService> _logger;

    public MaintenanceReminderTickerService(
        IServiceScopeFactory scopeFactory,
        DbConnectionManager connectionManager,
        ILogger<MaintenanceReminderTickerService> logger)
    {
        _scopeFactory = scopeFactory;
        _connectionManager = connectionManager;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await BackgroundDatabaseGate.WaitUntilConfiguredAsync(_connectionManager, _logger, nameof(MaintenanceReminderTickerService), stoppingToken);

        // Give the seeder/migrations a moment, and tolerate a cold database.
        await WaitForDatabaseAsync(stoppingToken);

        using var timer = new PeriodicTimer(TickPeriod);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var dispatcher = scope.ServiceProvider.GetRequiredService<MaintenanceReminderDispatcher>();
                    await dispatcher.DispatchDueRemindersAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Maintenance reminder tick failed.");
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
                await dbContext.AdminUsers
                    .AsNoTracking()
                    .AnyAsync(stoppingToken);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Maintenance ticker could not reach the database yet. Retrying in {Seconds}s.",
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
