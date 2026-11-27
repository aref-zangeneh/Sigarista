using Sugarista.Application.Customers.Commands.AdjustCustomerWallet;
using Sugarista.Application.Wallets.Commands.ApplyWalletTopUpPackage;
using Sugarista.Application.Wallets.Commands.UpdateWalletSettings;

namespace Sugarista.Application.UnitTests.Wallets;

public class WalletCommandValidatorTests
{
    private readonly AdjustCustomerWalletCommandValidator _adjustValidator = new();
    private readonly ApplyWalletTopUpPackageCommandValidator _packageValidator = new();
    private readonly UpdateWalletSettingsCommandValidator _settingsValidator = new();

    [Fact]
    public void AdjustWallet_ValidPositiveAndNegativeAmounts_PassValidation()
    {
        var topUp = new AdjustCustomerWalletCommand(Guid.NewGuid(), 100_000m, "شارژ حضوری");
        var debit = new AdjustCustomerWalletCommand(Guid.NewGuid(), -25_000m, "اصلاح خطای صندوق");

        Assert.True(_adjustValidator.Validate(topUp).IsValid);
        Assert.True(_adjustValidator.Validate(debit).IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1_000_000_000)]
    [InlineData(-1_000_000_000)]
    public void AdjustWallet_ZeroOrOutOfRangeAmount_FailsValidation(decimal amount)
    {
        var command = new AdjustCustomerWalletCommand(Guid.NewGuid(), amount, "test");
        var result = _adjustValidator.Validate(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void AdjustWallet_EmptyCustomerId_FailsValidation()
    {
        var command = new AdjustCustomerWalletCommand(Guid.Empty, 50_000m, "test");
        Assert.False(_adjustValidator.Validate(command).IsValid);
    }

    [Fact]
    public void ApplyTopUpPackage_ValidCommand_PassesValidation()
    {
        var command = new ApplyWalletTopUpPackageCommand(Guid.NewGuid(), Guid.NewGuid(), "شارژ بسته طلایی");
        Assert.True(_packageValidator.Validate(command).IsValid);
    }

    [Fact]
    public void ApplyTopUpPackage_MissingIdsOrTooLongNote_FailsValidation()
    {
        Assert.False(_packageValidator.Validate(new ApplyWalletTopUpPackageCommand(Guid.Empty, Guid.NewGuid())).IsValid);
        Assert.False(_packageValidator.Validate(new ApplyWalletTopUpPackageCommand(Guid.NewGuid(), Guid.Empty)).IsValid);
        Assert.False(_packageValidator.Validate(new ApplyWalletTopUpPackageCommand(Guid.NewGuid(), Guid.NewGuid(), new string('a', 301))).IsValid);
    }

    [Theory]
    [InlineData(true, 5, 100_000, 50_000, 90, 100, "Wallet")]
    [InlineData(false, 0, 0, 0, 0, 1, "Manual")]
    public void UpdateWalletSettings_ValidSettings_PassValidation(
        bool enabled,
        decimal cashbackPercent,
        decimal minOrder,
        decimal maxCashback,
        int expiryDays,
        int maxPayPercent,
        string refundMode)
    {
        var command = new UpdateWalletSettingsCommand(
            enabled,
            cashbackPercent,
            minOrder,
            maxCashback,
            expiryDays,
            maxPayPercent,
            refundMode);

        Assert.True(_settingsValidator.Validate(command).IsValid);
    }

    [Theory]
    [InlineData(-1, 0, 0, 0, 100, "Wallet")]
    [InlineData(101, 0, 0, 0, 100, "Wallet")]
    [InlineData(5, -1, 0, 0, 100, "Wallet")]
    [InlineData(5, 0, -1, 0, 100, "Wallet")]
    [InlineData(5, 0, 0, -1, 100, "Wallet")]
    [InlineData(5, 0, 0, 3651, 100, "Wallet")]
    [InlineData(5, 0, 0, 0, 0, "Wallet")]
    [InlineData(5, 0, 0, 0, 101, "Wallet")]
    [InlineData(5, 0, 0, 0, 100, "Cash")]
    public void UpdateWalletSettings_InvalidValues_FailValidation(
        decimal cashbackPercent,
        decimal minOrder,
        decimal maxCashback,
        int expiryDays,
        int maxPayPercent,
        string refundMode)
    {
        var command = new UpdateWalletSettingsCommand(
            true,
            cashbackPercent,
            minOrder,
            maxCashback,
            expiryDays,
            maxPayPercent,
            refundMode);

        Assert.False(_settingsValidator.Validate(command).IsValid);
    }
}
