using Microsoft.Extensions.Options;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Common.Options;

namespace Sugarista.Infrastructure.Payments;

public class MockPaymentGateway : IPaymentGateway
{
    private readonly PaymentOptions _options;

    public MockPaymentGateway(IOptions<PaymentOptions> options)
    {
        _options = options.Value;
    }

    public Task<PaymentRequestResult> RequestPaymentAsync(
        Guid orderId,
        int amount,
        string callbackUrl,
        string description,
        CancellationToken cancellationToken = default)
    {
        var authority = Guid.NewGuid().ToString("N");
        var baseUrl = _options.PublicBaseUrl.TrimEnd('/');
        var redirectUrl = $"{baseUrl}/?pay={orderId}&authority={authority}";

        return Task.FromResult(new PaymentRequestResult(authority, redirectUrl));
    }

    public Task<PaymentVerifyResult> VerifyPaymentAsync(
        string authority,
        int amount,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(authority))
        {
            return Task.FromResult(new PaymentVerifyResult(false, null, "Payment authority is missing."));
        }

        var referenceId = $"MOCK-{authority[..Math.Min(8, authority.Length)].ToUpperInvariant()}";
        return Task.FromResult(new PaymentVerifyResult(true, referenceId, "Mock payment verified."));
    }
}
