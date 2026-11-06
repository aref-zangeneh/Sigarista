using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class DrinkBuilderProfileConfiguration : IEntityTypeConfiguration<DrinkBuilderProfile>
{
    public void Configure(EntityTypeBuilder<DrinkBuilderProfile> builder)
    {
        builder.HasKey(profile => profile.Id);

        builder.Property(profile => profile.Name).HasMaxLength(150).IsRequired();

        // One builder profile per menu item; the item's size prices are the base.
        builder.HasIndex(profile => profile.MenuItemId).IsUnique();

        builder.HasOne(profile => profile.MenuItem)
            .WithMany()
            .HasForeignKey(profile => profile.MenuItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(profile => profile.Groups)
            .WithOne(group => group.Profile)
            .HasForeignKey(group => group.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class DrinkBuilderGroupConfiguration : IEntityTypeConfiguration<DrinkBuilderGroup>
{
    public void Configure(EntityTypeBuilder<DrinkBuilderGroup> builder)
    {
        builder.HasKey(group => group.Id);

        builder.Property(group => group.Name).HasMaxLength(150).IsRequired();

        builder.Property(group => group.InputType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(group => group.SelectionMode)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.HasMany(group => group.Options)
            .WithOne(option => option.Group)
            .HasForeignKey(option => option.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(group => new { group.ProfileId, group.DisplayOrder });
    }
}

public class DrinkBuilderOptionConfiguration : IEntityTypeConfiguration<DrinkBuilderOption>
{
    public void Configure(EntityTypeBuilder<DrinkBuilderOption> builder)
    {
        builder.HasKey(option => option.Id);

        builder.Property(option => option.Name).HasMaxLength(150).IsRequired();
        builder.Property(option => option.EnglishName).HasMaxLength(150);

        builder.Property(option => option.Unit)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(option => option.PricePerUnit).HasColumnType("numeric(18,2)");
        builder.Property(option => option.MinAmount).HasColumnType("numeric(18,3)");
        builder.Property(option => option.MaxAmount).HasColumnType("numeric(18,3)");
        builder.Property(option => option.Step).HasColumnType("numeric(18,3)");
        builder.Property(option => option.DefaultAmount).HasColumnType("numeric(18,3)");

        builder.HasIndex(option => new { option.GroupId, option.DisplayOrder });
    }
}
