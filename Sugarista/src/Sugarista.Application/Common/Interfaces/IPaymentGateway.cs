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
        decimal amount,
        string callbackUrl,
        string description,
        CancellationToken cancellationToken = default);

    Task<PaymentVerifyResult> VerifyPaymentAsync(
        string authority,
        decimal amount,
        CancellationToken cancellationToken = default);
}
