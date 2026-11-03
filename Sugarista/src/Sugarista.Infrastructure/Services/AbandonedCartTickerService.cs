using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sugarista.Application.Carts.Services;

namespace Sugarista.Infrastructure.Services;

/// <summary>
/// Background ticker for the abandoned-cart reminder sweep. Runs every half
/// hour (after a short startup delay) so a cart whose inactivity window elapsed
/// gets its comeback code promptly, without hammering the database.
/// </summary>
public class AbandonedCartTickerService : BackgroundService
{
    private static readonly TimeSpan TickPeriod = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AbandonedCartTickerService> _logger;

    public AbandonedCartTickerService(
        IServiceScopeFactory scopeFactory,
        ILogger<AbandonedCartTickerService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(StartupDelay, stoppingToken);

        using var timer = new PeriodicTimer(TickPeriod);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var dispatcher = scope.ServiceProvider.GetRequiredService<AbandonedCartReminderDispatcher>();
                    await dispatcher.DispatchDueRemindersAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Abandoned-cart reminder tick failed.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Host shutting down — expected on graceful stop.
        }
    }
}
