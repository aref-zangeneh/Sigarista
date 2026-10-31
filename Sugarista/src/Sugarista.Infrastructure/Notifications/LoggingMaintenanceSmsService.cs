using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Common.Options;

namespace Sugarista.Infrastructure.Notifications;

public class LoggingMaintenanceSmsService : IMaintenanceSmsSender
{
    private readonly ILogger<LoggingMaintenanceSmsService> _logger;
    private readonly SmsOptions _options;

    public LoggingMaintenanceSmsService(ILogger<LoggingMaintenanceSmsService> logger, IOptions<SmsOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Maintenance SMS (dev/log, provider {Provider}) to {Phone}: {Message}",
            _options.Provider,
            phoneNumber,
            message);

        return Task.CompletedTask;
    }
}
