namespace Sugarista.Application.Common.Interfaces;

public record PaymentRequestResult(
    string Authority,
    string RedirectUrl);

public record PaymentVerifyResult(
    bool IsSuccessful,
    string? ReferenceId,
    string? Message);

public interface IPaymentGateway
{
    Task<PaymentRequestResult> RequestPaymentAsync(
        Guid orderId,
        int amount,
        string callbackUrl,
        string description,
        CancellationToken cancellationToken = default);

    Task<PaymentVerifyResult> VerifyPaymentAsync(
        string authority,
        int amount,
        CancellationToken cancellationToken = default);
}
