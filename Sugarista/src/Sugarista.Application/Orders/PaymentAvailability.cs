using Sugarista.Application.Common.Options;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.Orders;

/// <summary>
/// Central guard for the payment gateway configuration. The in-memory mock
/// gateway is only meant for development/demo environments; enabling it in a
/// production deployment would let guests mark orders as paid without paying.
/// </summary>
public static class PaymentAvailability
{
    public static bool IsMock(PaymentOptions options) =>
        string.Equals(options.Provider, "Mock", StringComparison.OrdinalIgnoreCase);

    public static bool IsOnlinePaymentConfigured(PaymentOptions options) =>
        !IsMock(options) || options.AllowMockInProduction;

    public static void EnsureOnlinePaymentIsConfigured(PaymentOptions options)
    {
        if (IsOnlinePaymentConfigured(options))
        {
            return;
        }

        throw new DomainException(
            "درگاه پرداخت آنلاین در این محیط فعال نیست؛ لطفاً سفارش را حضوری ثبت کنید.");
    }
}
