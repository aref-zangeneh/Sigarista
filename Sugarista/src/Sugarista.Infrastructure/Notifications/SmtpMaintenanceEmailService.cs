using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Common.Options;

namespace Sugarista.Infrastructure.Notifications;

public class SmtpMaintenanceEmailService : IMaintenanceEmailSender
{
    private readonly ILogger<SmtpMaintenanceEmailService> _logger;
    private readonly EmailOptions _options;

    public SmtpMaintenanceEmailService(ILogger<SmtpMaintenanceEmailService> logger, IOptions<EmailOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public async Task SendAsync(string toAddress, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Host))
        {
            throw new InvalidOperationException("SMTP host is not configured (Email:Host).");
        }

        var fromAddress = MailAddressOrThrow(_options.FromAddress ?? _options.Username);
        var fromName = string.IsNullOrWhiteSpace(_options.FromName) ? "Sugarista" : _options.FromName;

        using var message = new MailMessage
        {
            From = new MailAddress(fromAddress, fromName, System.Text.Encoding.UTF8),
            Subject = subject,
            SubjectEncoding = System.Text.Encoding.UTF8,
            Body = htmlBody,
            BodyEncoding = System.Text.Encoding.UTF8,
            IsBodyHtml = true,
            Priority = MailPriority.Normal,
        };
        message.To.Add(new MailAddress(toAddress.Trim()));

        cancellationToken.ThrowIfCancellationRequested();

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.UseSsl,
        };

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            client.Credentials = new NetworkCredential(_options.Username, _options.Password ?? string.Empty);
        }

        await client.SendMailAsync(message, cancellationToken);

        _logger.LogInformation("Maintenance email sent to {To} via {Host}:{Port}.", toAddress, _options.Host, _options.Port);
    }

    private static string MailAddressOrThrow(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate) || !candidate.Contains('@', StringComparison.Ordinal))
        {
            throw new InvalidOperationException("A valid sender address is required (Email:FromAddress).");
        }

        return candidate.Trim();
    }
}
