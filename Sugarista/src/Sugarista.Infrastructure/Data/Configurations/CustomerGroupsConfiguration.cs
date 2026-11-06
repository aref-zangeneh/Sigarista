using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class CustomerGroupConfiguration : IEntityTypeConfiguration<CustomerGroup>
{
    public void Configure(EntityTypeBuilder<CustomerGroup> builder)
    {
        builder.HasKey(group => group.Id);

        builder.Property(group => group.Name).HasMaxLength(150).IsRequired();
        builder.Property(group => group.Description).HasMaxLength(500);
        builder.Property(group => group.DiscountPercent).HasPrecision(5, 2);
        builder.Property(group => group.EmailDomain).HasMaxLength(150);

        builder.HasIndex(group => group.Name).IsUnique();
        builder.HasIndex(group => group.DisplayOrder);

        builder.ToTable("CustomerGroups");
    }
}

public class CustomerGroupMembershipConfiguration : IEntityTypeConfiguration<CustomerGroupMembership>
{
    public void Configure(EntityTypeBuilder<CustomerGroupMembership> builder)
    {
        builder.HasKey(membership => membership.Id);

        // One row per customer per group: renewals update the same row.
        builder.HasIndex(membership => new { membership.CustomerId, membership.GroupId }).IsUnique();

        builder.HasOne(membership => membership.Customer)
            .WithMany()
            .HasForeignKey(membership => membership.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(membership => membership.Group)
            .WithMany()
            .HasForeignKey(membership => membership.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable("CustomerGroupMemberships");
    }
}

public class SubscriptionPackageConfiguration : IEntityTypeConfiguration<SubscriptionPackage>
{
    public void Configure(EntityTypeBuilder<SubscriptionPackage> builder)
    {
        builder.HasKey(package => package.Id);

        builder.Property(package => package.Name).HasMaxLength(150).IsRequired();
        builder.Property(package => package.Description).HasMaxLength(500);
        builder.Property(package => package.Price).HasPrecision(18, 2);
        builder.Property(package => package.AllowedCategoryIds).HasMaxLength(2000);

        builder.HasIndex(package => package.DisplayOrder);

        builder.ToTable("SubscriptionPackages");
    }
}

public class CustomerSubscriptionConfiguration : IEntityTypeConfiguration<CustomerSubscription>
{
    public void Configure(EntityTypeBuilder<CustomerSubscription> builder)
    {
        builder.HasKey(subscription => subscription.Id);

        builder.Property(subscription => subscription.PackageName).HasMaxLength(150).IsRequired();
        builder.Property(subscription => subscription.Price).HasPrecision(18, 2);
        builder.Property(subscription => subscription.AllowedCategoryIds).HasMaxLength(2000);

        builder.HasIndex(subscription => subscription.CustomerId);
        builder.HasIndex(subscription => new { subscription.CustomerId, subscription.Status });
        builder.HasIndex(subscription => subscription.ExpiresAt);

        builder.HasOne(subscription => subscription.Customer)
            .WithMany()
            .HasForeignKey(subscription => subscription.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(subscription => subscription.Package)
            .WithMany()
            .HasForeignKey(subscription => subscription.PackageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable("CustomerSubscriptions");
    }
}
