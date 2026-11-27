using MediatR;
using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Customers.Services;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Wallets.Commands.ExpireDueCashback;

public record ExpireDueCashbackResultDto(
    int AffectedWalletsCount,
    int ExpiredLotsCount);

public record ExpireDueCashbackCommand : IRequest<ExpireDueCashbackResultDto>;

public class ExpireDueCashbackCommandHandler
    : IRequestHandler<ExpireDueCashbackCommand, ExpireDueCashbackResultDto>
{
    private readonly IApplicationDbContext _dbContext;

    public ExpireDueCashbackCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ExpireDueCashbackResultDto> Handle(
        ExpireDueCashbackCommand request,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        var walletIds = await _dbContext.WalletTransactions
            .AsNoTracking()
            .Where(t => t.Type == WalletTransactionType.Cashback &&
                        t.ExpiresAt != null &&
                        t.ExpiresAt <= now)
            .Select(t => t.WalletId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (walletIds.Count == 0)
        {
            return new ExpireDueCashbackResultDto(0, 0);
        }

        var wallets = await _dbContext.CustomerWallets
            .Where(w => walletIds.Contains(w.Id) && w.Balance > 0m)
            .ToListAsync(cancellationToken);

        var affectedWallets = 0;
        var expiredLots = 0;

        foreach (var wallet in wallets)
        {
            var expired = await WalletService.ExpireDueCashbackAsync(_dbContext, wallet, cancellationToken);
            if (expired > 0)
            {
                affectedWallets++;
                expiredLots += expired;
            }
        }

        return new ExpireDueCashbackResultDto(affectedWallets, expiredLots);
    }
}
