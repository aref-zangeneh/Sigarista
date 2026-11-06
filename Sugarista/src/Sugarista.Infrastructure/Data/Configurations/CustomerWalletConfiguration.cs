using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class CustomerWalletConfiguration : IEntityTypeConfiguration<CustomerWallet>
{
    public void Configure(EntityTypeBuilder<CustomerWallet> builder)
    {
        builder.HasKey(wallet => wallet.Id);

        builder.Property(wallet => wallet.Balance).HasPrecision(18, 2);

        // One wallet per customer, created lazily by the wallet service.
        builder.HasIndex(wallet => wallet.CustomerId).IsUnique();

        builder.HasOne(wallet => wallet.Customer)
            .WithOne()
            .HasForeignKey<CustomerWallet>(wallet => wallet.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable("CustomerWallets");
    }
}

public class WalletTransactionConfiguration : IEntityTypeConfiguration<WalletTransaction>
{
    public void Configure(EntityTypeBuilder<WalletTransaction> builder)
    {
        builder.HasKey(transaction => transaction.Id);

        builder.Property(transaction => transaction.Amount).HasPrecision(18, 2);
        builder.Property(transaction => transaction.BalanceAfter).HasPrecision(18, 2);
        builder.Property(transaction => transaction.Description).HasMaxLength(500);

        builder.HasIndex(transaction => transaction.WalletId);
        builder.HasIndex(transaction => transaction.OrderId);
        builder.HasIndex(transaction => transaction.CreatedAt);

        builder.HasOne(transaction => transaction.Wallet)
            .WithMany()
            .HasForeignKey(transaction => transaction.WalletId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(transaction => transaction.Order)
            .WithMany()
            .HasForeignKey(transaction => transaction.OrderId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.ToTable("WalletTransactions");
    }
}
