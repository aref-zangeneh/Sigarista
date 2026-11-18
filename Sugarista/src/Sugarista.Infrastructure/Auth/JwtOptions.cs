namespace Sugarista.Infrastructure.Auth;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Default admin session lifetime: 30 days (43,200 minutes).</summary>
    public const int DefaultExpirationMinutes = 30 * 24 * 60;

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public string SigningKey { get; set; } = string.Empty;

    public int ExpirationMinutes { get; set; } = DefaultExpirationMinutes;
}
