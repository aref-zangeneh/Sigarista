using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.Property(a => a.Code).HasMaxLength(20).IsRequired();
        builder.Property(a => a.Name).HasMaxLength(200).IsRequired();
        builder.Property(a => a.NamePersian).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasIndex(a => a.Code).IsUnique();
    }
}

public class PartnerConfiguration : IEntityTypeConfiguration<Partner>
{
    public void Configure(EntityTypeBuilder<Partner> builder)
    {
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Email).HasMaxLength(200);
        builder.Property(p => p.Phone).HasMaxLength(50);
        builder.Property(p => p.Notes).HasMaxLength(1000);
        builder.Property(p => p.OwnershipPercentage).HasColumnType("numeric(8,4)");
    }
}

public class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> builder)
    {
        builder.Property(e => e.ReferenceNumber).HasMaxLength(30).IsRequired();
        builder.Property(e => e.SourceType).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(1000).IsRequired();
        builder.HasIndex(e => e.ReferenceNumber).IsUnique();
        builder.HasIndex(e => new { e.SourceType, e.SourceId }).IsUnique();
        builder.HasIndex(e => e.EntryDate);
    }
}

public class JournalLineConfiguration : IEntityTypeConfiguration<JournalLine>
{
    public void Configure(EntityTypeBuilder<JournalLine> builder)
    {
        builder.Property(l => l.Debit).HasColumnType("numeric(18,2)");
        builder.Property(l => l.Credit).HasColumnType("numeric(18,2)");
        builder.Property(l => l.Description).HasMaxLength(500);
        builder.HasOne(l => l.JournalEntry).WithMany(e => e.Lines).HasForeignKey(l => l.JournalEntryId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(l => l.Account).WithMany(a => a.JournalLines).HasForeignKey(l => l.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(l => l.Partner).WithMany().HasForeignKey(l => l.PartnerId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class PartnerCapitalTransactionConfiguration : IEntityTypeConfiguration<PartnerCapitalTransaction>
{
    public void Configure(EntityTypeBuilder<PartnerCapitalTransaction> builder)
    {
        builder.Property(t => t.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(t => t.Amount).HasColumnType("numeric(18,2)");
        builder.Property(t => t.Description).HasMaxLength(1000);
        builder.HasOne(t => t.Partner).WithMany(p => p.CapitalTransactions).HasForeignKey(t => t.PartnerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(t => t.JournalEntry).WithMany().HasForeignKey(t => t.JournalEntryId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class ProfitDistributionConfiguration : IEntityTypeConfiguration<ProfitDistribution>
{
    public void Configure(EntityTypeBuilder<ProfitDistribution> builder)
    {
        builder.Property(d => d.TotalNetProfit).HasColumnType("numeric(18,2)");
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasOne(d => d.JournalEntry).WithMany().HasForeignKey(d => d.JournalEntryId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class ProfitDistributionLineConfiguration : IEntityTypeConfiguration<ProfitDistributionLine>
{
    public void Configure(EntityTypeBuilder<ProfitDistributionLine> builder)
    {
        builder.Property(l => l.OwnershipPercentage).HasColumnType("numeric(8,4)");
        builder.Property(l => l.AllocatedAmount).HasColumnType("numeric(18,2)");
        builder.HasOne(l => l.ProfitDistribution).WithMany(d => d.Lines).HasForeignKey(l => l.ProfitDistributionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(l => l.Partner).WithMany().HasForeignKey(l => l.PartnerId).OnDelete(DeleteBehavior.Restrict);
    }
}
