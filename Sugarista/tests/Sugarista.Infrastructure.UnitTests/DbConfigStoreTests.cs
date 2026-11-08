using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Sugarista.Infrastructure.Data.DbConnection;

namespace Sugarista.Infrastructure.UnitTests;

public sealed class TestHostEnvironment : IHostEnvironment
{
    public TestHostEnvironment(string contentRootPath)
    {
        ContentRootPath = contentRootPath;
    }

    public string EnvironmentName { get; set; } = "Testing";
    public string ApplicationName { get; set; } = "Sugarista.Tests";
    public string ContentRootPath { get; set; }
    public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider("/");
}

public sealed class DbConfigStoreTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"sgr-dbcfg-{Guid.NewGuid():N}");
    private readonly DbConfigStore _store;

    public DbConfigStoreTests()
    {
        Directory.CreateDirectory(_tempDir);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DB_CONFIG_PATH"] = Path.Combine(_tempDir, "db-connection.json")
            })
            .Build();

        _store = new DbConfigStore(
            new TestHostEnvironment(_tempDir),
            configuration,
            NullLogger<DbConfigStore>.Instance);
    }

    [Fact]
    public void TryRead_WhenFileMissing_ReturnsNull()
    {
        Assert.Null(_store.TryRead());
    }

    [Fact]
    public void WriteThenRead_RoundTripsAllFields()
    {
        var settings = MakeSettings(password: "Sg-dbx9mKf-Aa1!");

        _store.Write(settings);
        var loaded = _store.TryRead();

        Assert.NotNull(loaded);
        Assert.Equal(settings.Host, loaded.Host);
        Assert.Equal(settings.Port, loaded.Port);
        Assert.Equal(settings.Database, loaded.Database);
        Assert.Equal(settings.Username, loaded.Username);
        Assert.Equal(settings.Password, loaded.Password);
        Assert.Equal(settings.SslMode, loaded.SslMode);
    }

    [Fact]
    public void TryRead_WhenJsonIsCorrupt_ReturnsNull()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_store.FilePath)!);
        File.WriteAllText(_store.FilePath, "{ not valid json !!");

        Assert.Null(_store.TryRead());
    }

    [Fact]
    public void Signature_ChangesWhenPasswordChanges()
    {
        var a = _store.Signature(MakeSettings(password: "first-password"));
        var b = _store.Signature(MakeSettings(password: "second-password"));

        Assert.NotEqual(a, b);
        Assert.Equal(a, _store.Signature(MakeSettings(password: "first-password")));
    }

    [Fact]
    public void Write_IsAtomic_TempFileDoesNotSurvive()
    {
        _store.Write(MakeSettings());

        Assert.False(File.Exists(_store.FilePath + ".tmp"));
        Assert.NotNull(_store.TryRead());
    }

    [Fact]
    public void FileExistsUnderscoreConfiguredPath_IsUsed()
    {
        Assert.EndsWith("db-connection.json", _store.FilePath);
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

    private static DbConnectionSettings MakeSettings(string password = "Sg-default-pass-Aa1!") =>
        new()
        {
            Host = "postgres",
            Port = 5432,
            Database = "sugarista",
            Username = "sugarista",
            Password = password
        };
}
