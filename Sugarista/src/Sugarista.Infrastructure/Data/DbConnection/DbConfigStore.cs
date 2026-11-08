using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Sugarista.Infrastructure.Data.DbConnection;

/// <summary>
/// Reads and writes <c>db-connection.json</c> — the application-owned database
/// configuration file. Writes are atomic (temp file + rename) so a process can
/// never be left with a partially-written file, and re-reads are cheap enough
/// to run per request (the connection manager watches for external edits).
/// </summary>
public sealed class DbConfigStore
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _filePath;
    private readonly ILogger<DbConfigStore> _logger;

    public DbConfigStore(
        IHostEnvironment environment,
        IConfiguration configuration,
        ILogger<DbConfigStore> logger)
    {
        var configured = configuration["DB_CONFIG_PATH"];
        _filePath = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(environment.ContentRootPath, "config", "db-connection.json")
            : Path.GetFullPath(configured);
        _logger = logger;
    }

    public string FilePath => _filePath;

    /// <summary>Reads the file; null when absent/corrupt. Never throws.</summary>
    public DbConnectionSettings? TryRead()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return null;
            }

            var json = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            return JsonSerializer.Deserialize<DbConnectionSettings>(json, JsonOptions);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Could not read the database connection file at {Path}; ignoring it.",
                _filePath);
            return null;
        }
    }

    public void Write(DbConnectionSettings settings)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        var tempFile = _filePath + ".tmp";

        // UTF-8 without BOM — other tools must be able to read it as plain JSON.
        File.WriteAllText(tempFile, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.Move(tempFile, _filePath, overwrite: true);

        _logger.LogInformation("Database connection settings written to {Path}.", _filePath);
    }

    /// <summary>Stable signature of the settings content, used to detect external edits.</summary>
    public string Signature(DbConnectionSettings settings)
    {
        var canonical = JsonSerializer.Serialize(settings, JsonOptions);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
