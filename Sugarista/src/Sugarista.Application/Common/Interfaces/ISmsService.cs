namespace Sugarista.Application.Common.Interfaces;

public interface ISmsService
{
    Task SendOrderReadyAsync(
        string phoneNumber,
        string trackingCode,
        string customerName,
        CancellationToken cancellationToken = default);
}
