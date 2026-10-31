using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Common.Options;

namespace Sugarista.Infrastructure.Notifications;

public class LoggingMaintenanceEmailService : IMaintenanceEmailSender
{
    private readonly ILogger<LoggingMaintenanceEmailService> _logger;
    private readonly EmailOptions _options;

    public LoggingMaintenanceEmailService(ILogger<LoggingMaintenanceEmailService> logger, IOptions<EmailOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public Task SendAsync(string toAddress, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Maintenance email (dev/log, provider {Provider}) to {To}: {Subject}\n{Body}",
            _options.Provider,
            toAddress,
            subject,
            htmlBody);

        return Task.CompletedTask;
    }
}
