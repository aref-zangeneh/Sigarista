using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Sugarista.Infrastructure.Data.DbConnection;

namespace Sugarista.Infrastructure.UnitTests;

public sealed class DbConnectionManagerTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"sgr-dbmgr-{Guid.NewGuid():N}");

    public DbConnectionManagerTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    private DbConnectionSettings Settings(string password = "Sg-dbpass-01-Aa1!") => new()
    {
        Host = "postgres",
        Port = 5432,
        Database = "sugarista",
        Username = "sugarista",
        Password = password
    };

    private DbConfigStore MakeStore(string? configPath = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DB_CONFIG_PATH"] = configPath ?? Path.Combine(_tempDir, "db-connection.json")
            })
            .Build();

        return new DbConfigStore(
            new TestHostEnvironment(_tempDir),
            configuration,
            NullLogger<DbConfigStore>.Instance);
    }

    private DbConnectionManager MakeManager(DbConfigStore store, string? fallback = null) =>
        new(store, fallback, NullLogger<DbConnectionManager>.Instance);

    [Fact]
    public void NoFileAndNoFallback_RunsInSetupMode()
    {
        var manager = MakeManager(MakeStore());

        Assert.False(manager.IsConfigured);
        Assert.Null(manager.CurrentSettings);
        Assert.Null(manager.GetDataSource());
    }

    [Fact]
    public void FallbackEnvironmentConnectionString_KeepsTheAppRunning()
    {
        var manager = MakeManager(MakeStore(), fallback: "Host=db;Port=5433;Database=sgr;Username=u;Password=p");

        Assert.True(manager.IsConfigured);

        var settings = manager.CurrentSettings!;
        Assert.Equal("db", settings.Host);
        Assert.Equal(5433, settings.Port);
        Assert.Equal("sgr", settings.Database);
        Assert.Equal("p", settings.Password);
    }

    [Fact]
    public void Configure_MakesConnectionLiveAndWritesFile()
    {
        var store = MakeStore();
        var manager = MakeManager(store);

        manager.Configure(Settings(password: "Sg-live-pass-Aa1!"));

        Assert.True(manager.IsConfigured);
        Assert.Equal("Sg-live-pass-Aa1!", manager.CurrentSettings!.Password);
        Assert.NotNull(store.TryRead());
        Assert.False(manager.GetDataSource() is null);
    }

    [Fact]
    public void Configure_WithInvalidValues_ThrowsAndLeavesStateEmpty()
    {
        var manager = MakeManager(MakeStore());

        Assert.Throws<InvalidOperationException>(() =>
            manager.Configure(new DbConnectionSettings { Host = "", Database = "", Username = "" }));

        Assert.False(manager.IsConfigured);
    }

    [Fact]
    public void FileEditedExternally_IsPickedUpOnNextDataSourceAccess()
    {
        var configPath = Path.Combine(_tempDir, "rotate-external.json");
        var store = MakeStore(configPath);
        var manager = MakeManager(store);

        manager.Configure(Settings(password: "Sg-old-pass-Aa1!"));

        // Simulate "someone told the site the new password by editing the file".
        store.Write(Settings(password: "Sg-new-pass-Aa1!"));

        var settings = manager.CurrentSettings!;
        Assert.Equal("Sg-new-pass-Aa1!", settings.Password);
    }

    [Fact]
    public void UpdatePassword_ChangesLivePasswordAndPersistsFile()
    {
        var store = MakeStore(Path.Combine(_tempDir, "rotate-inmem.json"));
        var manager = MakeManager(store);

        manager.Configure(Settings(password: "Sg-old-pass-Aa1!"));
        manager.UpdatePassword("Sg-fresh-pass-Aa1!");

        Assert.Equal("Sg-fresh-pass-Aa1!", manager.CurrentSettings!.Password);
        Assert.Equal("Sg-fresh-pass-Aa1!", store.TryRead()!.Password);
    }

    [Fact]
    public void UpdatePassword_WhenUnconfigured_ThrowsSetupModeError()
    {
        var manager = MakeManager(MakeStore());

        Assert.ThrowsAny<InvalidOperationException>(() => manager.UpdatePassword("Sg-any-pass-Aa1!"));
    }

    [Fact]
    public void CorruptFileEdit_DoesNotKillWorkingConnection()
    {
        var configPath = Path.Combine(_tempDir, "robust.json");
        var store = MakeStore(configPath);
        var manager = MakeManager(store);

        manager.Configure(Settings(password: "Sg-stable-pass-Aa1!"));
        File.WriteAllText(configPath, "garbage {");

        Assert.True(manager.IsConfigured);
        Assert.Equal("Sg-stable-pass-Aa1!", manager.CurrentSettings!.Password);
    }

    [Fact]
    public void BuildDataSource_RejectsBadPort()
    {
        var settings = Settings();
        settings.Port = 0;

        Assert.Throws<InvalidOperationException>(() => settings.BuildDataSource());
    }

    [Fact]
    public void SqlMode_InvalidValue_FallsBackToPrefer()
    {
        var settings = Settings();
        settings.SslMode = "not-a-mode";

        var builder = new Npgsql.NpgsqlConnectionStringBuilder(settings.ToConnectionString());
        Assert.Equal(Npgsql.SslMode.Prefer, builder.SslMode);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
        }
    }
}
