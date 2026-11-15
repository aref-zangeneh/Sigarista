using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Sugarista.Application.Sms;

namespace Sugarista.Infrastructure.Sms;

/// <summary>
/// DataProtection-backed credential encryption. Keys persist in the app's
/// config directory (same volume as db-connection.json) so ciphertext keeps
/// decrypting across container rebuilds.
/// </summary>
public sealed class SmsCredentialProtector : ISmsCredentialProtector
{
    private const string Purpose = "Sugarista.Sms.Credentials.v1";

    private readonly IDataProtector _protector;
    private readonly ILogger<SmsCredentialProtector> _logger;

    public SmsCredentialProtector(
        IDataProtectionProvider provider,
        ILogger<SmsCredentialProtector> logger)
    {
        _protector = provider.CreateProtector(Purpose);
        _logger = logger;
    }

    public string? Encrypt(string? plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
        {
            return null;
        }

        return _protector.Protect(plaintext);
    }

    public string? Decrypt(string? ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext))
        {
            return null;
        }

        try
        {
            return _protector.Unprotect(ciphertext);
        }
        catch (CryptographicException exception)
        {
            // Key rotation / volume loss — the stored secret is unrecoverable.
            _logger.LogError(
                exception,
                "An SMS provider credential could not be decrypted (key ring changed?). Re-enter the credential in the admin panel.");
            return null;
        }
    }
}
