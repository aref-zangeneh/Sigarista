using Sugarista.Application.Sms;
using Xunit;

namespace Sugarista.Application.UnitTests.Sms;

public class AdminSmsProvidersTests
{
    private readonly UpsertSmsProviderCommandValidator _validator = new();

    [Fact]
    public void Validator_AllowsEmptyApiKeyWhenUpdatingExistingSmsIrProvider()
    {
        var command = new UpsertSmsProviderCommand(
            Id: Guid.NewGuid(),
            Name: "SMS.ir اصلی",
            ProviderType: "SmsIr",
            BaseUrl: null,
            ApiKey: null,
            Username: null,
            Password: null,
            TimeoutSeconds: 30,
            ExtraSettingsJson: null,
            IsEnabled: true,
            IsDefault: true,
            SenderLines: [new SmsSenderLineInput("30001234", "خط اصلی", true)]);

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validator_RequiresApiKeyWhenCreatingNewSmsIrProvider()
    {
        var command = new UpsertSmsProviderCommand(
            Id: Guid.Empty,
            Name: "SMS.ir جدید",
            ProviderType: "SmsIr",
            BaseUrl: null,
            ApiKey: null,
            Username: null,
            Password: null,
            TimeoutSeconds: 30,
            ExtraSettingsJson: null,
            IsEnabled: true,
            IsDefault: false,
            SenderLines: []);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("کلید API"));
    }

    [Fact]
    public void Validator_RequiresUsernameAndPasswordForNewMelipayamakProvider()
    {
        var missingPass = new UpsertSmsProviderCommand(
            Id: Guid.Empty,
            Name: "ملی پیامک",
            ProviderType: "Melipayamak",
            BaseUrl: null,
            ApiKey: null,
            Username: "myuser",
            Password: null,
            TimeoutSeconds: 30,
            ExtraSettingsJson: null,
            IsEnabled: true,
            IsDefault: false,
            SenderLines: []);

        Assert.False(_validator.Validate(missingPass).IsValid);

        var complete = missingPass with { Password = "secret" };
        Assert.True(_validator.Validate(complete).IsValid);
    }

    [Fact]
    public void Validator_AllowsCredentiallessLoggingProviderOnCreate()
    {
        var command = new UpsertSmsProviderCommand(
            Id: Guid.Empty,
            Name: "لاگ تست",
            ProviderType: "Logging",
            BaseUrl: null,
            ApiKey: null,
            Username: null,
            Password: null,
            TimeoutSeconds: 30,
            ExtraSettingsJson: null,
            IsEnabled: true,
            IsDefault: false,
            SenderLines: []);

        Assert.True(_validator.Validate(command).IsValid);
    }
}
