namespace Sugarista.Application.Common.Options;

public class PaymentOptions
{
    public const string SectionName = "Payment";

    public string Provider { get; set; } = "Mock";

    public string PublicBaseUrl { get; set; } = "http://localhost";

    /// <summary>
    /// The mock gateway must never be reachable in production unless the owner
    /// explicitly accepts that orders will be marked paid without a real payment.
    /// </summary>
    public bool AllowMockInProduction { get; set; }
}
