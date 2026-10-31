namespace Sugarista.Application.Common.Options;

public class EmailOptions
{
    public const string SectionName = "Email";

    public string Provider { get; set; } = "Logging";

    public string? Host { get; set; }

    public int Port { get; set; } = 587;

    public string? FromAddress { get; set; }

    public string FromName { get; set; } = "شوگاریستا";

    public string? Username { get; set; }

    public string? Password { get; set; }

    public bool UseSsl { get; set; } = true;
}
