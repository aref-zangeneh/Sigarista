using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.UnitTests;

public class CustomerWalletTests
{
    [Fact]
    public void Constructor_WithEmptyCustomerId_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => new CustomerWallet(Guid.Empty));
    }

    [Fact]
    public void Credit_PositiveAmount_IncreasesBalance()
    {
        var wallet = new CustomerWallet(Guid.NewGuid());
        Assert.Equal(0m, wallet.Balance);

        wallet.Credit(150_000m);
        wallet.Credit(50_000m);

        Assert.Equal(200_000m, wallet.Balance);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-50_000)]
    public void Credit_NonPositiveAmount_ThrowsDomainException(decimal amount)
    {
        var wallet = new CustomerWallet(Guid.NewGuid());
        Assert.Throws<DomainException>(() => wallet.Credit(amount));
        Assert.Equal(0m, wallet.Balance);
    }

    [Fact]
    public void Debit_WithinBalance_DecreasesBalance()
    {
        var wallet = new CustomerWallet(Guid.NewGuid());
        wallet.Credit(200_000m);

        wallet.Debit(75_000m);

        Assert.Equal(125_000m, wallet.Balance);
    }

    [Fact]
    public void Debit_ExactBalance_LeavesZero()
    {
        var wallet = new CustomerWallet(Guid.NewGuid());
        wallet.Credit(100_000m);

        wallet.Debit(100_000m);

        Assert.Equal(0m, wallet.Balance);
    }

    [Fact]
    public void Debit_MoreThanBalance_ThrowsAndPreservesBalance()
    {
        var wallet = new CustomerWallet(Guid.NewGuid());
        wallet.Credit(100_000m);

        var ex = Assert.Throws<DomainException>(() => wallet.Debit(100_001m));

        Assert.Contains("موجودی کیف پول کافی نیست", ex.Message);
        Assert.Equal(100_000m, wallet.Balance);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10_000)]
    public void Debit_NonPositiveAmount_ThrowsDomainException(decimal amount)
    {
        var wallet = new CustomerWallet(Guid.NewGuid());
        wallet.Credit(50_000m);

        Assert.Throws<DomainException>(() => wallet.Debit(amount));
        Assert.Equal(50_000m, wallet.Balance);
    }

    [Fact]
    public void WalletTransaction_ZeroAmount_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => new WalletTransaction(
            Guid.NewGuid(),
            WalletTransactionType.Adjustment,
            0m,
            10_000m,
            "test"));
    }

    [Fact]
    public void WalletTransaction_NegativeBalanceAfter_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => new WalletTransaction(
            Guid.NewGuid(),
            WalletTransactionType.Purchase,
            -10_000m,
            -1m,
            "test"));
    }

    [Fact]
    public void WalletTransaction_BlankDescription_FallsBackToTypeName()
    {
        var tx = new WalletTransaction(
            Guid.NewGuid(),
            WalletTransactionType.TopUp,
            50_000m,
            50_000m,
            "   ");

        Assert.Equal("TopUp", tx.Description);
    }

    [Fact]
    public void WalletTransaction_MarkExpiryLogged_ClearsExpiresAt()
    {
        var expiresAt = DateTimeOffset.UtcNow.AddDays(30);
        var tx = new WalletTransaction(
            Guid.NewGuid(),
            WalletTransactionType.Cashback,
            25_000m,
            25_000m,
            "کش‌بک سفارش",
            expiresAt: expiresAt);

        Assert.Equal(expiresAt, tx.ExpiresAt);

        tx.MarkExpiryLogged();

        Assert.Null(tx.ExpiresAt);
    }

    [Fact]
    public void WalletTopUpPackage_ValidValues_UpdatesCleanly()
    {
        var pack = new WalletTopUpPackage(500_000m, 50_000m, displayOrder: 1, isEnabled: true);
        Assert.Equal(500_000m, pack.Amount);
        Assert.Equal(50_000m, pack.BonusAmount);
        Assert.True(pack.IsEnabled);

        pack.Update(1_000_000m, 150_000m, 2, false);
        Assert.Equal(1_000_000m, pack.Amount);
        Assert.Equal(150_000m, pack.BonusAmount);
        Assert.Equal(2, pack.DisplayOrder);
        Assert.False(pack.IsEnabled);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-1000, 0)]
    [InlineData(500_000, -1)]
    public void WalletTopUpPackage_InvalidAmounts_ThrowsDomainException(decimal amount, decimal bonus)
    {
        Assert.Throws<DomainException>(() => new WalletTopUpPackage(amount, bonus));
    }
}
