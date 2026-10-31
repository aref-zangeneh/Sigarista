namespace Sugarista.Application.Common.Interfaces;

/// <summary>
/// SMS channel for maintenance service reminders (dynamic recipients).
/// </summary>
public interface IMaintenanceSmsSender
{
    Task SendAsync(
        string phoneNumber,
        string message,
        CancellationToken cancellationToken = default);
}
