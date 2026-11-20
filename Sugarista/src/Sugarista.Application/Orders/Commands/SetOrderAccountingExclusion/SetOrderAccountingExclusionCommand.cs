using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Accounting.Services;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Orders.Commands.UpdateAdminOrder;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Orders.Commands.SetOrderAccountingExclusion;

public record SetOrderAccountingExclusionCommand(
    Guid OrderId,
    bool Exclude,
    string? Reason = null) : IRequest<AdminOrderDetailDto>;

public class SetOrderAccountingExclusionCommandValidator : AbstractValidator<SetOrderAccountingExclusionCommand>
{
    public SetOrderAccountingExclusionCommandValidator()
    {
        RuleFor(c => c.OrderId).NotEmpty();
        RuleFor(c => c.Reason).MaximumLength(300);
    }
}

public class SetOrderAccountingExclusionCommandHandler
    : IRequestHandler<SetOrderAccountingExclusionCommand, AdminOrderDetailDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAccountingService _accountingService;

    public SetOrderAccountingExclusionCommandHandler(
        IApplicationDbContext dbContext,
        IAccountingService accountingService)
    {
        _dbContext = dbContext;
        _accountingService = accountingService;
    }

    public async Task<AdminOrderDetailDto> Handle(
        SetOrderAccountingExclusionCommand request,
        CancellationToken cancellationToken)
    {
        var order = await _dbContext.Orders
            .Include(o => o.Lines)
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken)
            ?? throw new KeyNotFoundException("Order was not found.");

        if (request.Exclude)
        {
            order.ExcludeFromAccounting(request.Reason);
            await _accountingService.RemoveOrderJournalEntriesAsync(order.Id, cancellationToken);
        }
        else
        {
            order.IncludeInAccounting();
            if (order.Status == OrderStatus.Paid)
            {
                await _accountingService.PostOrderPaymentAsync(order, cancellationToken);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return AdminOrderMapper.ToDetail(order);
    }
}
