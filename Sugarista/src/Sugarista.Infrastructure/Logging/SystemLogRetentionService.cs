using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Sugarista.Infrastructure.Logging;

/// <summary>
/// Scheduled system-log retention. Serilog's <c>retainedFileCountLimit</c> only
/// prunes when a roll happens — on quiet days old day-files accumulate forever,
/// slowly filling the server's disk. This ticker periodically deletes every
/// day-file older than <c>LoggingInfrastructure:FileRetentionDays</c> (default
/// 14, 0 = disabled), using <see cref="SystemLogFileStore.DeleteOlderThanDays"/>,
/// which never touches audit files or the current day.
///
/// Ticks every 6 hours (cheap: one directory listing per tick) so a restarted
/// container catches up quickly instead of waiting a full day.
/// </summary>
public class SystemLogRetentionService(
    SystemLogFileStore logFileStore,
    LoggingInfrastructureOptions options,
    ILogger<SystemLogRetentionService> logger) : BackgroundService
{
    private static readonly TimeSpan TickPeriod = TimeSpan.FromHours(6);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(90);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.FileRetentionDays <= 0)
        {
            logger.LogInformation(
                "System-log retention job disabled (FileRetentionDays={FileRetentionDays}).",
                options.FileRetentionDays);
            return;
        }

        await Task.Delay(StartupDelay, stoppingToken);

        using var timer = new PeriodicTimer(TickPeriod);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    var result = logFileStore.DeleteOlderThanDays(options.FileRetentionDays);
                    if (result.DeletedFiles > 0)
                    {
                        logger.LogInformation(
                            "System-log retention removed {DeletedFiles} file(s) older than {Days} days, freed {FreedBytes} bytes.",
                            result.DeletedFiles, options.FileRetentionDays, result.FreedBytes);
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "System-log retention tick failed.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Host shutting down — expected on graceful stop.
        }
    }
}
