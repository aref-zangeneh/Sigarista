using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Sugarista.Application.Sms;
using Sugarista.Infrastructure.Sms;

namespace Sugarista.Infrastructure.UnitTests;

public sealed class SmsCredentialProtectorTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"sgr-keys-{Guid.NewGuid():N}");

    public SmsCredentialProtectorTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    private SmsCredentialProtector MakeProtector() =>
        new(
            new ServiceCollection()
                .AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(_tempDir))
                .SetApplicationName("Sugarista")
                .Services
                .BuildServiceProvider()
                .GetRequiredService<IDataProtectionProvider>(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SmsCredentialProtector>.Instance);

    [Fact]
    public void EncryptDecrypt_RoundTripsSecret()
    {
        var protector = MakeProtector();
        const string secret = "kavenegar-api-key-1234567890";

        var ciphertext = protector.Encrypt(secret);

        Assert.NotEqual(secret, ciphertext);
        Assert.False(ciphertext!.Contains(secret, StringComparison.Ordinal), "ciphertext must not contain the secret");
        Assert.Equal(secret, protector.Decrypt(ciphertext));
    }

    [Fact]
    public void Encrypt_NullOrEmpty_ReturnsNull()
    {
        var protector = MakeProtector();

        Assert.Null(protector.Encrypt(null));
        Assert.Null(protector.Encrypt(string.Empty));
    }

    [Fact]
    public void Decrypt_UndecryptableData_ReturnsNullWithoutThrowing()
    {
        var protector = MakeProtector();

        Assert.Null(protector.Decrypt("definitely-not-a-valid-payload"));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // best effort
        }
    }
}

public sealed class SmsGatewayStatusMappingTests
{
    [Fact]
    public void KavenegarStatus_MapsLifecycleCorrectly()
    {
        Assert.Equal(SmsOutcomeStatus.Sent, KavenegarGateway.MapStatus(4));
        Assert.Equal(SmsOutcomeStatus.Delivered, KavenegarGateway.MapStatus(5));
        Assert.Equal(SmsOutcomeStatus.Delivered, KavenegarGateway.MapStatus(8));
        Assert.Equal(SmsOutcomeStatus.Failed, KavenegarGateway.MapStatus(6));
        Assert.Equal(SmsOutcomeStatus.Failed, KavenegarGateway.MapStatus(13));
        Assert.Equal(SmsOutcomeStatus.Rejected, KavenegarGateway.MapStatus(11));
        Assert.Equal(SmsOutcomeStatus.Rejected, KavenegarGateway.MapStatus(14));
        Assert.Equal(SmsOutcomeStatus.Unknown, KavenegarGateway.MapStatus(99));
    }

    [Fact]
    public void AmbiguousTimeout_Marker_IsRecognizedByPolicyCheck()
    {
        Assert.True(SmsSendErrors.IsAmbiguousTimeout(SmsSendErrors.AmbiguousTimeout));
        Assert.False(SmsSendErrors.IsAmbiguousTimeout("خطای دیگر"));
        Assert.False(SmsSendErrors.IsAmbiguousTimeout(null));
    }
}
