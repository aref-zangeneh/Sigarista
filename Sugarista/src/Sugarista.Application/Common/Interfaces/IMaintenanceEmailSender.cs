namespace Sugarista.Application.Common.Interfaces;

/// <summary>A file attached to an outgoing e-mail (e.g. a scheduled Excel report).</summary>
public sealed record EmailAttachment(string FileName, string ContentType, byte[] Content);

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

    /// <summary>
    /// Sends an e-mail with attachments. Implementations that cannot attach
    /// files fall back to a plain message.
    /// </summary>
    async Task SendAsync(
        string toAddress,
        string subject,
        string htmlBody,
        IReadOnlyCollection<EmailAttachment> attachments,
        CancellationToken cancellationToken = default)
    {
        await SendAsync(toAddress, subject, htmlBody, cancellationToken);
    }
}
