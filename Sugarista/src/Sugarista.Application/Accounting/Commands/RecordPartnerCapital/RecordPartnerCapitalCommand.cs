using Sugarista.Application.Accounting.Services;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Accounting.Commands.RecordPartnerCapital;

public record RecordPartnerCapitalCommand(
    Guid PartnerId,
    string Type,
    decimal Amount,
    DateOnly TransactionDate,
    string? Description) : IRequest<Guid>;

public class RecordPartnerCapitalCommandValidator : AbstractValidator<RecordPartnerCapitalCommand>
{
    public RecordPartnerCapitalCommandValidator()
    {
        RuleFor(command => command.PartnerId).NotEmpty();
        RuleFor(command => command.Amount).GreaterThan(0);
        RuleFor(command => command.Type)
            .Must(type => Enum.TryParse<PartnerCapitalTransactionType>(type, true, out _))
            .WithMessage("Type must be Contribution or Withdrawal.");
        RuleFor(command => command.Description).MaximumLength(1000);
    }
}

public class RecordPartnerCapitalCommandHandler : IRequestHandler<RecordPartnerCapitalCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAccountingService _accountingService;

    public RecordPartnerCapitalCommandHandler(
        IApplicationDbContext dbContext,
        IAccountingService accountingService)
    {
        _dbContext = dbContext;
        _accountingService = accountingService;
    }

    public async Task<Guid> Handle(RecordPartnerCapitalCommand request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<PartnerCapitalTransactionType>(request.Type, true, out var type))
        {
            throw new InvalidOperationException("Invalid transaction type.");
        }

        _ = await _dbContext.Partners
            .AsNoTracking()
            .FirstOrDefaultAsync(partner => partner.Id == request.PartnerId && partner.IsActive, cancellationToken)
            ?? throw new KeyNotFoundException("Active partner was not found.");

        var transaction = new PartnerCapitalTransaction(
            request.PartnerId,
            type,
            request.Amount,
            request.TransactionDate,
            request.Description);

        _dbContext.PartnerCapitalTransactions.Add(transaction);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var journalEntryId = await _accountingService.PostPartnerCapitalAsync(transaction, cancellationToken);
        transaction.LinkJournalEntry(journalEntryId);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return transaction.Id;
    }
}
