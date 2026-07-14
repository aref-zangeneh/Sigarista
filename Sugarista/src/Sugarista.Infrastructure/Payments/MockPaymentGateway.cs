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
        decimal amount,
        string callbackUrl,
        string description,
        CancellationToken cancellationToken = default)
    {
        var authority = Guid.NewGuid().ToString("N");
        var redirectUrl = BuildRedirectUrl(orderId, authority, callbackUrl);

        return Task.FromResult(new PaymentRequestResult(authority, redirectUrl));
    }

    private string BuildRedirectUrl(Guid orderId, string authority, string callbackUrl)
    {
        if (!string.IsNullOrWhiteSpace(callbackUrl))
        {
            if (callbackUrl.Contains("authority=", StringComparison.OrdinalIgnoreCase))
            {
                return System.Text.RegularExpressions.Regex.Replace(
                    callbackUrl,
                    "authority=[^&]*",
                    $"authority={authority}",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            return callbackUrl.Contains('?')
                ? $"{callbackUrl}&authority={authority}"
                : $"{callbackUrl}?authority={authority}";
        }

        var baseUrl = _options.PublicBaseUrl.TrimEnd('/');
        return $"{baseUrl}/?pay={orderId}&authority={authority}";
    }

    public Task<PaymentVerifyResult> VerifyPaymentAsync(
        string authority,
        decimal amount,
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
