using Sugarista.Application.Common.Options;
using Sugarista.Application.Orders;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.UnitTests;

public class PaymentAvailabilityTests
{
    [Fact]
    public void EnsureOnlinePaymentIsConfigured_ShouldBlockMockGatewayByDefault()
    {
        var options = new PaymentOptions { Provider = "Mock", AllowMockInProduction = false };

        Assert.Throws<DomainException>(() => PaymentAvailability.EnsureOnlinePaymentIsConfigured(options));
        Assert.False(PaymentAvailability.IsOnlinePaymentConfigured(options));
    }

    [Fact]
    public void EnsureOnlinePaymentIsConfigured_ShouldAllowExplicitMockOptIn()
    {
        var options = new PaymentOptions { Provider = "Mock", AllowMockInProduction = true };

        PaymentAvailability.EnsureOnlinePaymentIsConfigured(options);
        Assert.True(PaymentAvailability.IsOnlinePaymentConfigured(options));
    }

    [Fact]
    public void EnsureOnlinePaymentIsConfigured_ShouldAllowRealGateway()
    {
        var options = new PaymentOptions { Provider = "Zarinpal", AllowMockInProduction = false };

        PaymentAvailability.EnsureOnlinePaymentIsConfigured(options);
        Assert.True(PaymentAvailability.IsOnlinePaymentConfigured(options));
    }
}
