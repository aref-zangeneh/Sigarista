using Sugarista.Domain.Entities;

namespace Sugarista.Application.Accounting.Commands.DraftProfitDistribution;

public record DraftProfitDistributionCommand(DateOnly PeriodStart, DateOnly PeriodEnd)
    : IRequest<ProfitDistributionDraftDto>;

public record ProfitDistributionDraftDto(
    Guid? ExistingDistributionId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal TotalNetProfit,
    IReadOnlyCollection<ProfitDistributionPartnerLineDto> Lines);

public record ProfitDistributionPartnerLineDto(
    Guid PartnerId,
    string PartnerName,
    decimal OwnershipPercentage,
    decimal AllocatedAmount);

public class DraftProfitDistributionCommandHandler
    : IRequestHandler<DraftProfitDistributionCommand, ProfitDistributionDraftDto>
{
    private readonly IApplicationDbContext _dbContext;

    public DraftProfitDistributionCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ProfitDistributionDraftDto> Handle(
        DraftProfitDistributionCommand request,
        CancellationToken cancellationToken)
    {
        if (request.PeriodEnd < request.PeriodStart)
        {
            throw new InvalidOperationException("Period end cannot be before period start.");
        }

        var existing = await _dbContext.ProfitDistributions
            .AsNoTracking()
            .Include(distribution => distribution.Lines)
            .ThenInclude(line => line.Partner)
            .FirstOrDefaultAsync(
                distribution =>
                    distribution.PeriodStart == request.PeriodStart
                    && distribution.PeriodEnd == request.PeriodEnd
                    && distribution.Status == ProfitDistributionStatus.Draft,
                cancellationToken);

        if (existing is not null)
        {
            return new ProfitDistributionDraftDto(
                existing.Id,
                existing.PeriodStart,
                existing.PeriodEnd,
                existing.TotalNetProfit,
                existing.Lines.Select(line => new ProfitDistributionPartnerLineDto(
                    line.PartnerId,
                    line.Partner.Name,
                    line.OwnershipPercentage,
                    line.AllocatedAmount)).ToArray());
        }

        var (_, _, netProfit) = await AccountingLedgerHelper.GetPeriodProfitAsync(
            _dbContext, request.PeriodStart, request.PeriodEnd, cancellationToken);

        var partners = await _dbContext.Partners
            .AsNoTracking()
            .Where(partner => partner.IsActive)
            .OrderBy(partner => partner.Name)
            .ToListAsync(cancellationToken);

        if (partners.Count == 0)
        {
            throw new InvalidOperationException("Add at least one active partner before distributing profit.");
        }

        var totalOwnership = partners.Sum(partner => partner.OwnershipPercentage);
        if (totalOwnership <= 0)
        {
            throw new InvalidOperationException("Partner ownership percentages must total more than zero.");
        }

        var lines = partners.Select(partner =>
        {
            var allocated = Math.Round(netProfit * partner.OwnershipPercentage / totalOwnership, 2);
            return new ProfitDistributionPartnerLineDto(
                partner.Id,
                partner.Name,
                partner.OwnershipPercentage,
                allocated);
        }).ToArray();

        var roundedTotal = lines.Sum(line => line.AllocatedAmount);
        if (lines.Length > 0 && roundedTotal != netProfit)
        {
            var last = lines[^1];
            lines[^1] = last with { AllocatedAmount = last.AllocatedAmount + (netProfit - roundedTotal) };
        }

        return new ProfitDistributionDraftDto(null, request.PeriodStart, request.PeriodEnd, netProfit, lines);
    }
}
