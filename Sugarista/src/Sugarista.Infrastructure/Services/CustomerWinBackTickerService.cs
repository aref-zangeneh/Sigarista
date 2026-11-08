using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sugarista.Application.Customers.Services;
using Sugarista.Infrastructure.Data.DbConnection;

namespace Sugarista.Infrastructure.Services;

/// <summary>
/// Hourly background ticker that sends automatic win-back discount SMS codes to
/// customers who have not placed a paid order for longer than the configured
/// win-back window. A customer gets at most one win-back per inactivity window.
/// </summary>
public class CustomerWinBackTickerService : BackgroundService
{
    private static readonly TimeSpan TickPeriod = TimeSpan.FromHours(1);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(20);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DbConnectionManager _connectionManager;
    private readonly ILogger<CustomerWinBackTickerService> _logger;

    public CustomerWinBackTickerService(
        IServiceScopeFactory scopeFactory,
        DbConnectionManager connectionManager,
        ILogger<CustomerWinBackTickerService> logger)
    {
        _scopeFactory = scopeFactory;
        _connectionManager = connectionManager;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await BackgroundDatabaseGate.WaitUntilConfiguredAsync(_connectionManager, _logger, nameof(CustomerWinBackTickerService), stoppingToken);

        // Give the host a moment to settle before the first sweep.
        await Task.Delay(StartupDelay, stoppingToken);

        using var timer = new PeriodicTimer(TickPeriod);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var dispatcher = scope.ServiceProvider.GetRequiredService<CustomerWinBackDispatcher>();
                    await dispatcher.DispatchDueWinBacksAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Customer win-back dispatch tick failed.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Host shutting down — expected on graceful stop.
        }
    }
}
