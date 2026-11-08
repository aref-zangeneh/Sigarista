using Microsoft.Extensions.Logging;
using Npgsql;

namespace Sugarista.Infrastructure.Data.DbConnection;

/// <summary>
/// Singleton owner of the live <see cref="NpgsqlDataSource"/>.
///
/// WordPress-style behavior:
/// <list type="bullet">
///   <item>Database connection lives in <c>db-connection.json</c> (written by the
///        first-run setup page). No connection string needs to be baked into the
///        environment or the code.</item>
///   <item>The configuration file is re-read on demand, so editing it externally
///        (e.g. replacing the password on the server) changes the connection
///        <strong>without restarting the site</strong>.</item>
///   <item><see cref="UpdatePassword"/> rotates the password in-place on the pooled
///        data source (existing pool connections keep serving; every new physical
///        connection uses the new password) and persists it to the file.</item>
/// </list>
/// A missing configuration means "setup mode": the data source stays null and the
/// setup gate middleware blocks everything except the setup and health endpoints.
/// </summary>
public sealed class DbConnectionManager
{
    private readonly DbConfigStore _store;
    private readonly string? _fallbackConnectionString;
    private readonly ILogger<DbConnectionManager> _logger;
    private readonly object _sync = new();

    private NpgsqlDataSource? _dataSource;
    private DbConnectionSettings? _settings;
    private string? _signature;
    private bool _usingFallback;
    private bool _fallbackLogged;

    public DbConnectionManager(
        DbConfigStore store,
        string? fallbackConnectionString,
        ILogger<DbConnectionManager> logger)
    {
        _store = store;
        _fallbackConnectionString = fallbackConnectionString;
        _logger = logger;
    }

    /// <summary>Path of the configuration file (for the setup page / diagnostics).</summary>
    public string ConfigFilePath => _store.FilePath;

    /// <summary>Current settings backing the live data source, or null in setup mode.</summary>
    public DbConnectionSettings? CurrentSettings
    {
        get
        {
            lock (_sync)
            {
                GetDataSourceOrThrowSetupMode();

                return _settings;
            }
        }
    }

    private bool _seededOnce;

    /// <summary>
    /// True after this process has run migrations + seeding at least once.
    /// Background services wait on this, not merely on configuration, so a
    /// freshly configured database never receives "relation does not exist".
    /// </summary>
    public bool IsSeeded => _seededOnce && IsConfigured;

    /// <summary>Called by the seeder when migrations + seed finished in this process.</summary>
    public void MarkSeeded() => Volatile.Write(ref _seededOnce, true);

    /// <summary>True once a database connection is established (file or environment).</summary>
    public bool IsConfigured
    {
        get
        {
            lock (_sync)
            {
                return GetDataSourceOrThrowSetupMode() is not null;
            }
        }
    }

    /// <summary>
    /// Live connection source. Re-reads the configuration file on each call, so both
    /// an external edit of the file and <see cref="UpdatePassword"/> are picked up
    /// immediately by the next request. Null while the database is unconfigured.
    /// </summary>
    public NpgsqlDataSource? GetDataSource()
    {
        lock (_sync)
        {
            return GetDataSourceOrThrowSetupMode();
        }
    }

    /// <summary>
    /// First-run configuration coming from the setup page: validates, connects,
    /// persists to the JSON file and makes the data source live immediately.
    /// </summary>
    public DbConnectionSettings Configure(DbConnectionSettings settings)
    {
        var invalid = settings.ValidationMessage;
        if (invalid is not null)
        {
            throw new InvalidOperationException(invalid);
        }

        lock (_sync)
        {
            // If building fails (e.g. malformed values), nothing has changed yet.
            var dataSource = settings.BuildDataSource();

            _dataSource?.Dispose();
            _dataSource = dataSource;
            _settings = settings;
            _signature = _store.Signature(settings);
            _usingFallback = false;
            _fallbackLogged = false;
            _store.Write(settings);

            _logger.LogInformation(
                "Database connection configured via setup page: {Host}:{Port}/{Database} (file: {Path}).",
                settings.Host, settings.Port, settings.Database, _store.FilePath);

            return settings;
        }
    }

    /// <summary>
    /// Rotates the password in place: existing pooled connections keep serving,
    /// every newly created physical connection authenticates with
    /// <paramref name="newPassword"/>. The new value is persisted to the
    /// configuration file so it survives restarts. Call this AFTER the password
    /// has actually been changed in PostgreSQL (ALTER USER) — or only when the new
    /// password was already set there and successfully tested.
    /// </summary>
    public void UpdatePassword(string newPassword)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newPassword);

        lock (_sync)
        {
            var dataSource = GetDataSourceOrThrowSetupMode()
                ?? throw new InvalidOperationException(
                    "دیتابیس هنوز تنظیم نشده است؛ ابتدا تنظیم اولیه را انجام دهید.");

            var settings = _settings!
                .WithPassword(newPassword)
                ?? throw new InvalidOperationException("Connection details are unavailable.");

            // Npgsql 10: the Password property sets the password used for every
            // new physical connection, keeping existing pool connections alive
            // (equivalent to the old UpdatePassword of Npgsql 8/9).
            dataSource.Password = newPassword;
            _settings = settings;
            _signature = _store.Signature(settings);
            _store.Write(settings);

            _logger.LogInformation(
                "Database password rotated in place; new password persisted to {Path}.",
                _store.FilePath);
        }
    }

    /// <summary>Blocks until a database configuration exists (poll, no file watcher races).</summary>
    public async Task<bool> WaitUntilConfiguredAsync(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (IsConfigured)
            {
                return true;
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Opens a fresh connection with the given settings and blinks <c>SELECT 1</c>.
    /// Used by the setup page for "test" and for validating a rotated password.
    /// </summary>
    public static async Task TestConnectionAsync(
        DbConnectionSettings settings,
        CancellationToken cancellationToken)
    {
        await using var dataSource = settings.BuildDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1";
        await command.ExecuteScalarAsync(cancellationToken);
    }

    // Internal: must be called under _sync.
    private NpgsqlDataSource? GetDataSourceOrThrowSetupMode()
    {
        var fileSettings = _store.TryRead();
        if (fileSettings is not null && fileSettings.IsValid)
        {
            var signature = _store.Signature(fileSettings);

            if (_dataSource is null || _signature != signature || _usingFallback)
            {
                _dataSource?.Dispose();
                _dataSource = fileSettings.BuildDataSource();
                _settings = fileSettings;
                _signature = signature;
                _usingFallback = false;

                _logger.LogInformation(
                    "Database connection loaded from {Path}: {Host}:{Port}/{Database}.",
                    _store.FilePath, fileSettings.Host, fileSettings.Port, fileSettings.Database);
            }

            return _dataSource;
        }

        if (fileSettings is not null)
        {
            // File exists but its values are broken: keep serving with whatever is
            // loaded rather than killing a working site over a bad edit.
            _logger.LogError(
                "Database connection file at {Path} contains invalid values; keeping the current connection.",
                _store.FilePath);

            fileSettings = null;
        }

        // No (usable) file: fall back to the environment connection string, but only
        // until the file-based configuration appears — the file always wins.
        if (!_usingFallback && _dataSource is null && !string.IsNullOrEmpty(_fallbackConnectionString))
        {
            try
            {
                _dataSource = new NpgsqlDataSourceBuilder(_fallbackConnectionString).Build();
                _settings = DbConnectionSettings.FromConnectionString(_fallbackConnectionString);
                _signature = null;
                _usingFallback = true;

                if (!_fallbackLogged)
                {
                    _logger.LogInformation(
                        "Database connection from ConnectionStrings:DefaultConnection (config file absent).");
                    _fallbackLogged = true;
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "The environment database connection string is invalid; staying in setup mode.");
                _dataSource = null;
                _settings = null;
            }
        }

        return _dataSource;
    }
}
