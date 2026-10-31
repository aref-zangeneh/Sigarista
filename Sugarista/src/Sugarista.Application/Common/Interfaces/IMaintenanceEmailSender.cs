namespace Sugarista.Application.Common.Interfaces;

/// <summary>
/// Email channel for maintenance service reminders (dynamic recipients).
/// </summary>
public interface IMaintenanceEmailSender
{
    Task SendAsync(
        string toAddress,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default);
}
