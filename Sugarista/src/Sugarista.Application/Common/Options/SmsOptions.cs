namespace Sugarista.Application.Common.Options;

public class SmsOptions
{
    public const string SectionName = "Sms";

    public string Provider { get; set; } = "Logging";

    public string? ApiKey { get; set; }

    public string? Sender { get; set; }

    public string CafeName { get; set; } = "شوگاریستا";
}
