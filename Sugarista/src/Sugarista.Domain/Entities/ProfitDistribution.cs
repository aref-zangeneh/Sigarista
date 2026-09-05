using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public class ProfitDistribution : BaseAuditableEntity
{
    private ProfitDistribution()
    {
    }

    public ProfitDistribution(DateOnly periodStart, DateOnly periodEnd, decimal totalNetProfit)
    {
        if (periodEnd < periodStart)
        {
            throw new DomainException("Period end cannot be before period start.");
        }

        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        TotalNetProfit = totalNetProfit;
        Status = ProfitDistributionStatus.Draft;
    }

    public DateOnly PeriodStart { get; private set; }

    public DateOnly PeriodEnd { get; private set; }

    public decimal TotalNetProfit { get; private set; }

    public ProfitDistributionStatus Status { get; private set; }

    public DateTimeOffset? FinalizedAt { get; private set; }

    public Guid? JournalEntryId { get; private set; }

    public JournalEntry? JournalEntry { get; private set; }

    public ICollection<ProfitDistributionLine> Lines { get; private set; } = [];

    public void SetLines(IReadOnlyCollection<ProfitDistributionLine> lines)
    {
        if (Status != ProfitDistributionStatus.Draft)
        {
            throw new DomainException("Only draft distributions can be updated.");
        }

        Lines.Clear();
        foreach (var line in lines)
        {
            Lines.Add(line);
        }
    }

    public void Finalize(Guid journalEntryId)
    {
        if (Status == ProfitDistributionStatus.Finalized)
        {
            throw new DomainException("Distribution is already finalized.");
        }

        if (Lines.Count == 0)
        {
            throw new DomainException("Distribution must contain partner lines.");
        }

        Status = ProfitDistributionStatus.Finalized;
        FinalizedAt = DateTimeOffset.UtcNow;
        JournalEntryId = journalEntryId;
    }
}

public class ProfitDistributionLine : BaseEntity
{
    private ProfitDistributionLine()
    {
    }

    public ProfitDistributionLine(
        Guid profitDistributionId,
        Guid partnerId,
        decimal ownershipPercentage,
        decimal allocatedAmount)
    {
        if (profitDistributionId == Guid.Empty || partnerId == Guid.Empty)
        {
            throw new DomainException("Distribution and partner are required.");
        }

        ProfitDistributionId = profitDistributionId;
        PartnerId = partnerId;
        OwnershipPercentage = ownershipPercentage;
        AllocatedAmount = allocatedAmount;
    }

    public Guid ProfitDistributionId { get; private set; }

    public ProfitDistribution ProfitDistribution { get; private set; } = null!;

    public Guid PartnerId { get; private set; }

    public Partner Partner { get; private set; } = null!;

    public decimal OwnershipPercentage { get; private set; }

    public decimal AllocatedAmount { get; private set; }
}
