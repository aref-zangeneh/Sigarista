using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Common.Options;

namespace Sugarista.Infrastructure.Notifications;

public class LoggingSmsService : ISmsService
{
    private readonly ILogger<LoggingSmsService> _logger;
    private readonly SmsOptions _options;

    public LoggingSmsService(ILogger<LoggingSmsService> logger, IOptions<SmsOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public Task SendOrderReadyAsync(
        string phoneNumber,
        string trackingCode,
        string customerName,
        CancellationToken cancellationToken = default)
    {
        var message = BuildMessage(trackingCode, customerName, _options.CafeName);
        _logger.LogInformation(
            "SMS (dev/log) to {Phone}: {Message}",
            phoneNumber,
            message);

        return Task.CompletedTask;
    }

    internal static string BuildMessage(string trackingCode, string customerName, string cafeName)
    {
        return $"{customerName} عزیز، سفارش {trackingCode} آماده تحویل است. لطفاً برای دریافت به {cafeName} مراجعه کنید.";
    }
}
