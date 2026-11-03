using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class LoyaltyTransactionConfiguration : IEntityTypeConfiguration<LoyaltyTransaction>
{
    public void Configure(EntityTypeBuilder<LoyaltyTransaction> builder)
    {
        builder.HasKey(transaction => transaction.Id);

        builder.Property(transaction => transaction.Points).HasPrecision(18, 2);
        builder.Property(transaction => transaction.BalanceAfter).HasPrecision(18, 2);
        builder.Property(transaction => transaction.Description).HasMaxLength(500);

        builder.HasIndex(transaction => transaction.CustomerId);
        builder.HasIndex(transaction => transaction.OrderId);
        builder.HasIndex(transaction => transaction.CreatedAt);

        builder.HasOne(transaction => transaction.Customer)
            .WithMany(customer => customer.LoyaltyTransactions)
            .HasForeignKey(transaction => transaction.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(transaction => transaction.Order)
            .WithMany()
            .HasForeignKey(transaction => transaction.OrderId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.ToTable("LoyaltyTransactions");
    }
}
