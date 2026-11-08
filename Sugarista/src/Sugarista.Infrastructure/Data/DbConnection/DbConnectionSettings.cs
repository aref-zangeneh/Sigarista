using System.Text.Json.Serialization;
using Npgsql;

namespace Sugarista.Infrastructure.Data.DbConnection;

/// <summary>
/// Database connection details kept by the application itself (WordPress-style):
/// written to <c>config/db-connection.json</c> on first run and kept there so the
/// password can be rotated later without touching code or environment files.
/// </summary>
public sealed class DbConnectionSettings
{
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 5432;

    public string Database { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    /// <summary>Npgsql SSL mode name: Disable, Prefer, Require, VerifyCA, VerifyFull.</summary>
    public string SslMode { get; set; } = "Prefer";

    /// <summary>Persian validation message when the values cannot be used, otherwise null.</summary>
    [JsonIgnore]
    public string? ValidationMessage
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Host))
            {
                return "آدرس سرور دیتابیس (Host) وارد نشده است.";
            }

            if (Port is < 1 or > 65535)
            {
                return "پورت دیتابیس باید عددی بین ۱ تا ۶۵۵۳۵ باشد.";
            }

            if (string.IsNullOrWhiteSpace(Database))
            {
                return "نام دیتابیس وارد نشده است.";
            }

            if (string.IsNullOrWhiteSpace(Username))
            {
                return "نام کاربری دیتابیس وارد نشده است.";
            }

            if (!string.IsNullOrEmpty(Password) && Password.Length > 512)
            {
                return "رمز عبور طولانی‌تر از حد مجاز است.";
            }

            return null;
        }
    }

    public DbConnectionSettings WithPassword(string newPassword) => new()
    {
        Host = Host,
        Port = Port,
        Database = Database,
        Username = Username,
        Password = newPassword,
        SslMode = SslMode
    };

    /// <summary>Connection is ready to be used.</summary>
    public bool IsValid => ValidationMessage is null;

    /// <summary>Builds a validated-but-not-connected NpgsqlDataContext (no network I/O).</summary>
    public NpgsqlDataSource BuildDataSource()
    {
        var invalid = ValidationMessage;
        if (invalid is not null)
        {
            throw new InvalidOperationException(invalid);
        }

        return new NpgsqlDataSourceBuilder(ToConnectionString()).Build();
    }

    public string ToConnectionString()
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = Host.Trim(),
            Port = Port,
            Database = Database.Trim(),
            Username = Username.Trim(),
            Password = Password ?? string.Empty,
            SslMode = ParseSslMode(SslMode),
            ApplicationName = "Sugarista.Api"
        };

        return builder.ConnectionString;
    }

    /// <summary>Derives editable settings from an existing connection string (e.g. the environment variable).</summary>
    public static DbConnectionSettings FromConnectionString(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);

        return new DbConnectionSettings
        {
            Host = builder.Host ?? "localhost",
            Port = builder.Port,
            Database = builder.Database ?? string.Empty,
            Username = builder.Username ?? string.Empty,
            Password = builder.Password ?? string.Empty,
            SslMode = builder.SslMode.ToString()
        };
    }

    private static Npgsql.SslMode ParseSslMode(string? value) =>
        Enum.TryParse<Npgsql.SslMode>(value, ignoreCase: true, out var mode)
        && mode is Npgsql.SslMode.Disable
            or Npgsql.SslMode.Allow
            or Npgsql.SslMode.Prefer
            or Npgsql.SslMode.Require
            or Npgsql.SslMode.VerifyCA
            or Npgsql.SslMode.VerifyFull
            ? mode
            : Npgsql.SslMode.Prefer;
}
