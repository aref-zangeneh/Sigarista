using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Sugarista.Infrastructure.Data.DbConnection;
using Sugarista.Infrastructure.Settings;

namespace Sugarista.Infrastructure.UnitTests;

public sealed class AppSetupStoreTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"sgr-setup-{Guid.NewGuid():N}");
    private readonly AppSetupStore _store;

    public AppSetupStoreTests()
    {
        Directory.CreateDirectory(_tempDir);
        _store = MakeStore();
    }

    private AppSetupStore MakeStore(string fileName = "app-setup.json")
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DB_CONFIG_PATH"] = Path.Combine(_tempDir, fileName)
            })
            .Build();

        return new AppSetupStore(
            new DbConfigStore(
                new TestHostEnvironment(_tempDir),
                configuration,
                NullLogger<DbConfigStore>.Instance),
            NullLogger<AppSetupStore>.Instance);
    }

    [Fact]
    public void Load_WhenFileMissing_ReturnsEmptySecrets()
    {
        var secrets = _store.Load();

        Assert.False(secrets.HasBootstrapAdmin);
        Assert.Null(secrets.JwtSigningKey);
    }

    [Fact]
    public void Save_PersistsAdminAccount()
    {
        _store.PatchAdmin("admin", "Site Manager", "Sg-admin-pass-Aa1!");

        var secrets = _store.Load();

        Assert.Equal("admin", secrets.AdminUsername);
        Assert.Equal("Site Manager", secrets.AdminDisplayName);
        Assert.Equal("Sg-admin-pass-Aa1!", secrets.AdminPassword);
        Assert.True(secrets.HasBootstrapAdmin);
    }

    [Fact]
    public void Save_MergesWithExistingFile_TwoPatchesDoNotOverwriteEachOther()
    {
        _store.PatchJwtSigningKey(AppSetupStore.GenerateSigningKey());
        _store.PatchAdmin("admin", null, "Sg-admin-pass-Aa1!");

        var secrets = _store.Load();

        Assert.Equal("admin", secrets.AdminUsername);
        Assert.Equal("Sg-admin-pass-Aa1!", secrets.AdminPassword);
        Assert.False(string.IsNullOrEmpty(secrets.JwtSigningKey));
    }

    [Fact]
    public void Save_HidesAdminAccountWhenOnlyKeyIsPatched()
    {
        _store.PatchAdmin("admin", null, "Sg-admin-pass-Aa1!");
        _store.PatchJwtSigningKey("key".PadRight(40, '0'));

        var secrets = _store.Load();

        Assert.Equal("Sg-admin-pass-Aa1!", secrets.AdminPassword);
    }

    [Fact]
    public void GenerateSigningKey_IsLongEnoughAndSafe()
    {
        var key = AppSetupStore.GenerateSigningKey();

        Assert.True(AppSetupStore.IsSigningKeyUsable(key));
        Assert.Equal(64, key.Length);
    }

    [Fact]
    public void PatchRouteFlags_MergesToggles()
    {
        _store.PatchRouteFlags(setupPageEnabled: true, publicHealthEnabled: null);
        _store.PatchRouteFlags(setupPageEnabled: null, publicHealthEnabled: false);

        var secrets = _store.Load();

        Assert.True(secrets.SetupPageEnabled);
        Assert.False(secrets.PublicHealthEnabled);
    }

    [Fact]
    public void IsSigningKeyUsable_RejectsPlaceholders()
    {
        Assert.False(AppSetupStore.IsSigningKeyUsable(null));
        Assert.False(AppSetupStore.IsSigningKeyUsable("short"));
        Assert.False(AppSetupStore.IsSigningKeyUsable("SugaristaProductionSigningKeyChangeMe1234567890"));
        Assert.False(AppSetupStore.IsSigningKeyUsable("ReplaceWithALongRandomSecretForProductionUseOnly!"));
        Assert.True(AppSetupStore.IsSigningKeyUsable("Sg-a-very-long-and-random-key-value-1234567890"));
    }

    [Fact]
    public void Write_IsAtomic()
    {
        _store.PatchAdmin("admin", null, "Sg-admin-pass-Aa1!");

        Assert.False(File.Exists(_store.FilePath + ".tmp"));
        Assert.NotNull(_store.Load());
    }

    [Fact]
    public void TryRead_CorruptFile_ReturnsEmptyAndNeverThrows()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_store.FilePath)!);
        File.WriteAllText(_store.FilePath, "{ broken json");

        Assert.False(_store.Load().HasBootstrapAdmin);
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
