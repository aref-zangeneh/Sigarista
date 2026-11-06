using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class WalletTopUpPackageConfiguration : IEntityTypeConfiguration<WalletTopUpPackage>
{
    public void Configure(EntityTypeBuilder<WalletTopUpPackage> builder)
    {
        builder.HasKey(package => package.Id);

        builder.Property(package => package.Amount).HasPrecision(18, 2);
        builder.Property(package => package.BonusAmount).HasPrecision(18, 2);

        builder.HasIndex(package => package.DisplayOrder);

        builder.ToTable("WalletTopUpPackages");
    }
}

public class WalletTransactionExpiryConfiguration : IEntityTypeConfiguration<WalletTransaction>
{
    public void Configure(EntityTypeBuilder<WalletTransaction> builder)
    {
        builder.Property(transaction => transaction.ExpiresAt);
        builder.HasIndex(transaction => transaction.ExpiresAt);
    }
}
