using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class AbandonedCartConfiguration : IEntityTypeConfiguration<AbandonedCart>
{
    public void Configure(EntityTypeBuilder<AbandonedCart> builder)
    {
        builder.HasKey(cart => cart.Id);

        builder.Property(cart => cart.CartToken)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(cart => cart.CustomerName)
            .HasMaxLength(200);

        builder.Property(cart => cart.PhoneNumber)
            .HasMaxLength(30);

        builder.Property(cart => cart.Email)
            .HasMaxLength(200);

        builder.Property(cart => cart.ItemsJson)
            .IsRequired();

        builder.Property(cart => cart.ItemsTotal)
            .HasPrecision(18, 2);

        builder.Property(cart => cart.ReminderCode)
            .HasMaxLength(64);

        builder.Property(cart => cart.ReminderChannel)
            .HasMaxLength(32);

        // The reminder sweep filters on (Status, LastActivityAt) and the
        // storefront upserts the open row for a token.
        builder.HasIndex(cart => new { cart.Status, cart.LastActivityAt });
        builder.HasIndex(cart => new { cart.CartToken, cart.Status });

        builder.ToTable("AbandonedCarts");
    }
}
