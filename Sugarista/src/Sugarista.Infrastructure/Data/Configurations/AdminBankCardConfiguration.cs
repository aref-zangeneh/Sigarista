using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class AdminBankCardConfiguration : IEntityTypeConfiguration<AdminBankCard>
{
    public void Configure(EntityTypeBuilder<AdminBankCard> builder)
    {
        builder.ToTable("AdminBankCards");

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(x => x.BankName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.CardHolderName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(x => x.CardNumber)
            .IsRequired()
            .HasMaxLength(24);

        builder.Property(x => x.ShebaNumber)
            .HasMaxLength(34);

        builder.Property(x => x.CardImageUrl)
            .HasMaxLength(500);

        builder.Property(x => x.CardTheme)
            .IsRequired()
            .HasMaxLength(40)
            .HasDefaultValue("ocean");

        builder.HasIndex(x => x.IsActive);
        builder.HasIndex(x => x.IsDefault);
        builder.HasIndex(x => x.DisplayOrder);
    }
}
