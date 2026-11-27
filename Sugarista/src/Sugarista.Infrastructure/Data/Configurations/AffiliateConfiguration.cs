using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class AffiliateTierConfiguration : IEntityTypeConfiguration<AffiliateTier>
{
    public void Configure(EntityTypeBuilder<AffiliateTier> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name).HasMaxLength(120).IsRequired();
        builder.Property(t => t.MinMonthlySales).HasPrecision(18, 2);
        builder.Property(t => t.CommissionPercentage).HasPrecision(5, 2);
        builder.Property(t => t.CreditBonusPercentage).HasPrecision(5, 2);

        builder.HasIndex(t => t.Name).IsUnique();
        builder.HasIndex(t => t.MinMonthlySales);

        builder.ToTable("AffiliateTiers");
    }
}

public class AffiliateConfiguration : IEntityTypeConfiguration<Affiliate>
{
    public void Configure(EntityTypeBuilder<Affiliate> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.ShebaNumber).HasMaxLength(32);
        builder.Property(a => a.CardNumber).HasMaxLength(24);
        builder.Property(a => a.CashBalance).HasPrecision(18, 2);
        builder.Property(a => a.WalletCreditBalance).HasPrecision(18, 2);
        builder.Property(a => a.CustomCommissionRate).HasPrecision(5, 2);

        builder.HasIndex(a => a.UserId).IsUnique();
        builder.HasIndex(a => a.TierId);
        builder.HasIndex(a => a.Status);

        builder.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Tier)
            .WithMany(t => t.Affiliates)
            .HasForeignKey(a => a.TierId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.ToTable("Affiliates");
    }
}

public class AffiliateLinkConfiguration : IEntityTypeConfiguration<AffiliateLink>
{
    public void Configure(EntityTypeBuilder<AffiliateLink> builder)
    {
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Slug).HasMaxLength(120).IsRequired();
        builder.Property(l => l.SubId).HasMaxLength(120);
        builder.Property(l => l.TargetUrl).HasMaxLength(1000).IsRequired();

        builder.HasIndex(l => l.AffiliateId);
        builder.HasIndex(l => l.Slug);
        builder.HasIndex(l => new { l.Slug, l.SubId }).IsUnique();

        builder.HasOne(l => l.Affiliate)
            .WithMany(a => a.Links)
            .HasForeignKey(l => l.AffiliateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable("AffiliateLinks");
    }
}

public class AffiliateCouponConfiguration : IEntityTypeConfiguration<AffiliateCoupon>
{
    public void Configure(EntityTypeBuilder<AffiliateCoupon> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Code).HasMaxLength(64).IsRequired();
        builder.Property(c => c.DiscountValue).HasPrecision(18, 2);
        builder.Property(c => c.MaxDiscountAmount).HasPrecision(18, 2);

        builder.HasIndex(c => c.Code).IsUnique();
        builder.HasIndex(c => c.AffiliateId);
        builder.HasIndex(c => c.IsActive);

        builder.HasOne(c => c.Affiliate)
            .WithMany(a => a.Coupons)
            .HasForeignKey(c => c.AffiliateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable("AffiliateCoupons");
    }
}

public class ReferralClickConfiguration : IEntityTypeConfiguration<ReferralClick>
{
    public void Configure(EntityTypeBuilder<ReferralClick> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.IpAddress).HasMaxLength(64);
        builder.Property(c => c.UserAgent).HasMaxLength(512);

        builder.HasIndex(c => c.TrackingToken).IsUnique();
        builder.HasIndex(c => c.LinkId);
        builder.HasIndex(c => c.CreatedAt);
        builder.HasIndex(c => c.ExpiresAt);

        builder.HasOne(c => c.Link)
            .WithMany(l => l.Clicks)
            .HasForeignKey(c => c.LinkId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable("ReferralClicks");
    }
}

public class AffiliateCommissionConfiguration : IEntityTypeConfiguration<AffiliateCommission>
{
    public void Configure(EntityTypeBuilder<AffiliateCommission> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.OrderTotalAmount).HasPrecision(18, 2);
        builder.Property(c => c.EligibleAmount).HasPrecision(18, 2);
        builder.Property(c => c.CommissionAmount).HasPrecision(18, 2);
        builder.Property(c => c.RejectionReason).HasMaxLength(500);

        builder.HasIndex(c => c.OrderId);
        builder.HasIndex(c => new { c.AffiliateId, c.OrderId }).IsUnique();
        builder.HasIndex(c => new { c.AffiliateId, c.Status, c.CreatedAt });

        builder.HasOne(c => c.Affiliate)
            .WithMany(a => a.Commissions)
            .HasForeignKey(c => c.AffiliateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Order)
            .WithMany()
            .HasForeignKey(c => c.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable("AffiliateCommissions");
    }
}

public class AffiliatePayoutConfiguration : IEntityTypeConfiguration<AffiliatePayout>
{
    public void Configure(EntityTypeBuilder<AffiliatePayout> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Amount).HasPrecision(18, 2);
        builder.Property(p => p.TrackingCode).HasMaxLength(120);
        builder.Property(p => p.ReceiptImageUrl).HasMaxLength(1000);

        builder.HasIndex(p => p.AffiliateId);
        builder.HasIndex(p => p.Status);
        builder.HasIndex(p => p.TrackingCode);
        builder.HasIndex(p => p.ProcessedAt);

        builder.HasOne(p => p.Affiliate)
            .WithMany(a => a.Payouts)
            .HasForeignKey(p => p.AffiliateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable("AffiliatePayouts");
    }
}

public class CategoryCommissionConfiguration : IEntityTypeConfiguration<CategoryCommission>
{
    public void Configure(EntityTypeBuilder<CategoryCommission> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.CommissionPercentage).HasPrecision(5, 2);

        builder.HasIndex(c => c.CategoryId).IsUnique();

        builder.HasOne(c => c.Category)
            .WithMany()
            .HasForeignKey(c => c.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable("CategoryCommissions");
    }
}

public class AffiliateMediaAssetConfiguration : IEntityTypeConfiguration<AffiliateMediaAsset>
{
    public void Configure(EntityTypeBuilder<AffiliateMediaAsset> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Title).HasMaxLength(200).IsRequired();
        builder.Property(m => m.FileUrl).HasMaxLength(1000);
        builder.Property(m => m.ThumbnailUrl).HasMaxLength(1000);
        builder.Property(m => m.CaptionText).HasMaxLength(4000);

        builder.HasIndex(m => new { m.IsActive, m.DisplayOrder });

        builder.ToTable("AffiliateMediaAssets");
    }
}
