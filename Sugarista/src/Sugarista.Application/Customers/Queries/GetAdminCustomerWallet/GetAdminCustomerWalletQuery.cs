using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Customers.Queries.GetAdminCustomerWallet;

public record AdminWalletMovementDto(
    Guid Id,
    WalletTransactionType Type,
    decimal Amount,
    decimal BalanceAfter,
    string Description,
    Guid? OrderId,
    DateTimeOffset CreatedAt);

public record AdminCustomerWalletDto(
    Guid CustomerId,
    string CustomerName,
    string PhoneNumber,
    decimal Balance,
    IReadOnlyCollection<AdminWalletMovementDto> Transactions);

public record GetAdminCustomerWalletQuery(Guid CustomerId) : IRequest<AdminCustomerWalletDto?>;

public class GetAdminCustomerWalletQueryHandler : IRequestHandler<GetAdminCustomerWalletQuery, AdminCustomerWalletDto?>
{
    private readonly IApplicationDbContext _dbContext;

    public GetAdminCustomerWalletQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AdminCustomerWalletDto?> Handle(GetAdminCustomerWalletQuery request, CancellationToken cancellationToken)
    {
        var customer = await _dbContext.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(entry => entry.Id == request.CustomerId, cancellationToken);

        if (customer is null)
        {
            return null;
        }

        var wallet = await _dbContext.CustomerWallets
            .AsNoTracking()
            .FirstOrDefaultAsync(entry => entry.CustomerId == customer.Id, cancellationToken);

        var transactions = wallet is null
            ? []
            : await _dbContext.WalletTransactions
                .AsNoTracking()
                .Where(transaction => transaction.WalletId == wallet.Id)
                .OrderByDescending(transaction => transaction.CreatedAt)
                .Take(50)
                .Select(transaction => new AdminWalletMovementDto(
                    transaction.Id,
                    transaction.Type,
                    transaction.Amount,
                    transaction.BalanceAfter,
                    transaction.Description,
                    transaction.OrderId,
                    transaction.CreatedAt))
                .ToListAsync(cancellationToken);

        return new AdminCustomerWalletDto(
            customer.Id,
            customer.PersianName,
            customer.PhoneNumber,
            wallet?.Balance ?? 0m,
            transactions);
    }
}
