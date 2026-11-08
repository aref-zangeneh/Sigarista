using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sugarista.Infrastructure.Data.DbConnection;

namespace Sugarista.Infrastructure.Settings;

/// <summary>Server-side secrets the application creates for itself (WordPress-style).</summary>
public sealed class AppSetupSecrets
{
    /// <summary>JWT bearer signing key. Generated on first run when the environment does not provide a strong one.</summary>
    public string? JwtSigningKey { get; set; }

    /// <summary>Admin panel username chosen on the first-run setup page.</summary>
    public string? AdminUsername { get; set; }

    public string? AdminDisplayName { get; set; }

    /// <summary>Admin panel password chosen on the first-run setup page (used for seeding only).</summary>
    public string? AdminPassword { get; set; }

    [JsonIgnore]
    public bool HasBootstrapAdmin =>
        !string.IsNullOrWhiteSpace(AdminUsername) && !string.IsNullOrWhiteSpace(AdminPassword);
}

/// <summary>
/// Stores <c>app-setup.json</c> next to <c>db-connection.json</c>. Values are merged
/// into the existing file so first-run writes never lose earlier secrets.
/// </summary>
public sealed class AppSetupStore
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _filePath;
    private readonly ILogger<AppSetupStore> _logger;

    public AppSetupStore(DbConfigStore dbConfigStore, ILogger<AppSetupStore> logger)
    {
        var directory = Path.GetDirectoryName(dbConfigStore.FilePath);
        _filePath = string.IsNullOrEmpty(directory)
            ? "app-setup.json"
            : Path.Combine(directory, "app-setup.json");
        _logger = logger;
    }

    public string FilePath => _filePath;

    public AppSetupSecrets Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return new AppSetupSecrets();
            }

            var json = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new AppSetupSecrets();
            }

            var secrets = JsonSerializer.Deserialize<AppSetupSecrets>(json, JsonOptions);
            return secrets ?? new AppSetupSecrets();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not read app setup secrets at {Path}; ignoring the file.", _filePath);
            return new AppSetupSecrets();
        }
    }

    /// <summary>Merges the given values into the existing file (null keeps the stored value).</summary>
    public void Save(AppSetupSecrets values)
    {
        var merged = Load();
        merged.JwtSigningKey = values.JwtSigningKey ?? merged.JwtSigningKey;
        merged.AdminUsername = values.AdminUsername ?? merged.AdminUsername;
        merged.AdminDisplayName = values.AdminDisplayName ?? merged.AdminDisplayName;
        merged.AdminPassword = values.AdminPassword ?? merged.AdminPassword;

        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(merged, JsonOptions);
        var tempFile = _filePath + ".tmp";

        // No BOM, atomic rename — same discipline as db-connection.json.
        File.WriteAllText(tempFile, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.Move(tempFile, _filePath, overwrite: true);

        _logger.LogInformation("App setup secrets saved to {Path}.", _filePath);
    }

    /// <summary>Merges the given values into the existing file (null keeps the stored value).</summary>
    public void PatchAdmin(string? username, string? displayName, string? password)
    {
        Save(new AppSetupSecrets
        {
            AdminUsername = username,
            AdminDisplayName = displayName,
            AdminPassword = password
        });
    }

    /// <summary>Merges-only the JWT key into the file.</summary>
    public void PatchJwtSigningKey(string signingKey) =>
        Save(new AppSetupSecrets { JwtSigningKey = signingKey });

    /// <summary>128 bits of entropy, URL-safe, ~43 printable characters with no placeholder words.</summary>
    public static string GenerateSigningKey()
    {
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
            .TrimEnd('=')
            .Replace('+', 'A')
            .Replace('/', 'B');

        return key;
    }

    /// <summary>True when the signing key is acceptable for production use.</summary>
    public static bool IsSigningKeyUsable(string? signingKey)
    {
        if (string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < 32)
        {
            return false;
        }

        return !signingKey.Contains("ChangeMe", StringComparison.OrdinalIgnoreCase)
               && !signingKey.Contains("ChangeInProduction", StringComparison.OrdinalIgnoreCase)
               && !signingKey.Contains("ReplaceWith", StringComparison.OrdinalIgnoreCase);
    }
}
