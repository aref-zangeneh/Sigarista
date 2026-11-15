namespace Sugarista.Application.Sms;

/// <summary>
/// Encrypts/decrypts provider credentials at rest (DataProtection-backed in
/// Infrastructure). Ciphertext is what the database stores; plaintext exists
/// only in memory for the duration of a provider call.
/// </summary>
public interface ISmsCredentialProtector
{
    /// <summary>null/empty passes through as null; otherwise returns ciphertext.</summary>
    string? Encrypt(string? plaintext);

    /// <summary>null/empty passes through as null; undecryptable data becomes null (never throws).</summary>
    string? Decrypt(string? ciphertext);
}
