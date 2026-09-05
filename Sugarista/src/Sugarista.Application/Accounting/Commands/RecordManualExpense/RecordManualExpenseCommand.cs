using Sugarista.Application.Accounting.Services;

namespace Sugarista.Application.Accounting.Commands.RecordManualExpense;

public record RecordManualExpenseCommand(
    DateOnly EntryDate,
    decimal Amount,
    string Description,
    bool PaidFromCash) : IRequest<Guid>;

public class RecordManualExpenseCommandValidator : AbstractValidator<RecordManualExpenseCommand>
{
    public RecordManualExpenseCommandValidator()
    {
        RuleFor(command => command.Amount).GreaterThan(0);
        RuleFor(command => command.Description).NotEmpty().MaximumLength(1000);
    }
}

public class RecordManualExpenseCommandHandler : IRequestHandler<RecordManualExpenseCommand, Guid>
{
    private readonly IAccountingService _accountingService;

    public RecordManualExpenseCommandHandler(IAccountingService accountingService)
    {
        _accountingService = accountingService;
    }

    public Task<Guid> Handle(RecordManualExpenseCommand request, CancellationToken cancellationToken) =>
        _accountingService.PostManualExpenseAsync(
            request.EntryDate,
            request.Amount,
            request.Description,
            request.PaidFromCash,
            cancellationToken);
}
