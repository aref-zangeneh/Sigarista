using Sugarista.Application.Accounting.Services;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Accounting.Commands.FinalizeProfitDistribution;

public record FinalizeProfitDistributionCommand(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    IReadOnlyCollection<ProfitDistributionLineRequest>? Lines = null) : IRequest<Guid>;

public record ProfitDistributionLineRequest(Guid PartnerId, decimal AllocatedAmount);

public class FinalizeProfitDistributionCommandHandler : IRequestHandler<FinalizeProfitDistributionCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAccountingService _accountingService;
    private readonly IMediator _mediator;

    public FinalizeProfitDistributionCommandHandler(
        IApplicationDbContext dbContext,
        IAccountingService accountingService,
        IMediator mediator)
    {
        _dbContext = dbContext;
        _accountingService = accountingService;
        _mediator = mediator;
    }

    public async Task<Guid> Handle(FinalizeProfitDistributionCommand request, CancellationToken cancellationToken)
    {
        var draft = await _mediator.Send(
            new Commands.DraftProfitDistribution.DraftProfitDistributionCommand(
                request.PeriodStart,
                request.PeriodEnd),
            cancellationToken);

        var distribution = await _dbContext.ProfitDistributions
            .Include(entry => entry.Lines)
            .FirstOrDefaultAsync(
                entry =>
                    entry.PeriodStart == request.PeriodStart
                    && entry.PeriodEnd == request.PeriodEnd
                    && entry.Status == ProfitDistributionStatus.Draft,
                cancellationToken);

        if (distribution is null)
        {
            distribution = new ProfitDistribution(request.PeriodStart, request.PeriodEnd, draft.TotalNetProfit);
            _dbContext.ProfitDistributions.Add(distribution);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var lineRequests = request.Lines?.Count > 0
            ? request.Lines
            : draft.Lines.Select(line => new ProfitDistributionLineRequest(line.PartnerId, line.AllocatedAmount)).ToArray();

        var partners = await _dbContext.Partners
            .AsNoTracking()
            .Where(partner => lineRequests.Select(line => line.PartnerId).Contains(partner.Id))
            .ToDictionaryAsync(partner => partner.Id, cancellationToken);

        var distributionLines = lineRequests
            .Select(line =>
            {
                if (!partners.TryGetValue(line.PartnerId, out var partner))
                {
                    throw new KeyNotFoundException("Partner was not found.");
                }

                return new ProfitDistributionLine(
                    distribution.Id,
                    partner.Id,
                    partner.OwnershipPercentage,
                    line.AllocatedAmount);
            })
            .ToArray();

        var allocatedTotal = distributionLines.Sum(line => line.AllocatedAmount);
        if (allocatedTotal != draft.TotalNetProfit)
        {
            throw new InvalidOperationException("Allocated amounts must equal net profit for the period.");
        }

        if (distribution.Lines.Count > 0)
        {
            foreach (var existingLine in distribution.Lines.ToList())
            {
                _dbContext.ProfitDistributionLines.Remove(existingLine);
            }

            distribution.SetLines([]);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        distribution.SetLines(distributionLines);
        foreach (var line in distributionLines)
        {
            _dbContext.ProfitDistributionLines.Add(line);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var journalEntryId = await _accountingService.PostProfitDistributionAsync(distribution, cancellationToken);
        distribution.Finalize(journalEntryId);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return distribution.Id;
    }
}
