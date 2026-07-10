namespace Sugarista.Application.Common.Options;

public class PaymentOptions
{
    public const string SectionName = "Payment";

    public string Provider { get; set; } = "Mock";

    public string PublicBaseUrl { get; set; } = "http://localhost";
}
