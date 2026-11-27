using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Extensions;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Common.Models;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Wallets.Queries.GetAdminWallets;

public record AdminWalletListItemDto(
    Guid CustomerId,
    Guid? WalletId,
    string CustomerName,
    string PhoneNumber,
    bool IsActive,
    string? MembershipTierName,
    decimal Balance,
    decimal TotalTopUp,
    decimal TotalCashback,
    decimal TotalSpent,
    int TransactionCount,
    DateTimeOffset? LastTransactionAt);

public record GetAdminWalletsQuery(
    int Page = 1,
    int PageSize = 25,
    string? Search = null,
    string? BalanceFilter = null,
    decimal? MinBalance = null,
    decimal? MaxBalance = null,
    string? SortBy = null) : IRequest<PaginatedList<AdminWalletListItemDto>>;

public class GetAdminWalletsQueryHandler : IRequestHandler<GetAdminWalletsQuery, PaginatedList<AdminWalletListItemDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetAdminWalletsQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PaginatedList<AdminWalletListItemDto>> Handle(
        GetAdminWalletsQuery request,
        CancellationToken cancellationToken)
    {
        var pagination = new PaginationQuery(request.Page, request.PageSize, request.Search);

        var customersQuery = _dbContext.Customers
            .AsNoTracking()
            .Include(c => c.MembershipTier)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(pagination.Search))
        {
            var search = pagination.Search.Trim().ToLowerInvariant();
            customersQuery = customersQuery.Where(c =>
                c.PersianName.ToLower().Contains(search) ||
                c.PhoneNumber.Contains(search));
        }

        var joined =
            from customer in customersQuery
            join wallet in _dbContext.CustomerWallets.AsNoTracking()
                on customer.Id equals wallet.CustomerId into walletGroup
            from wallet in walletGroup.DefaultIfEmpty()
            select new
            {
                CustomerId = customer.Id,
                WalletId = wallet != null ? (Guid?)wallet.Id : null,
                CustomerName = customer.PersianName,
                PhoneNumber = customer.PhoneNumber,
                IsActive = customer.IsActive,
                MembershipTierName = customer.MembershipTier != null ? customer.MembershipTier.Name : null,
                Balance = wallet != null ? wallet.Balance : 0m,
                TotalTopUp = wallet != null
                    ? _dbContext.WalletTransactions
                        .Where(t => t.WalletId == wallet.Id && t.Type == WalletTransactionType.TopUp)
                        .Sum(t => (decimal?)t.Amount) ?? 0m
                    : 0m,
                TotalCashback = wallet != null
                    ? _dbContext.WalletTransactions
                        .Where(t => t.WalletId == wallet.Id && t.Type == WalletTransactionType.Cashback)
                        .Sum(t => (decimal?)t.Amount) ?? 0m
                    : 0m,
                TotalSpent = wallet != null
                    ? -(_dbContext.WalletTransactions
                        .Where(t => t.WalletId == wallet.Id && t.Type == WalletTransactionType.Purchase)
                        .Sum(t => (decimal?)t.Amount) ?? 0m)
                    : 0m,
                TransactionCount = wallet != null
                    ? _dbContext.WalletTransactions.Count(t => t.WalletId == wallet.Id)
                    : 0,
                LastTransactionAt = wallet != null
                    ? _dbContext.WalletTransactions
                        .Where(t => t.WalletId == wallet.Id)
                        .Select(t => (DateTimeOffset?)t.CreatedAt)
                        .DefaultIfEmpty()
                        .Max()
                    : null,
            };

        var balanceFilter = (request.BalanceFilter ?? string.Empty).Trim().ToLowerInvariant();
        if (balanceFilter == "positive")
        {
            joined = joined.Where(x => x.Balance > 0m);
        }
        else if (balanceFilter == "zero")
        {
            joined = joined.Where(x => x.Balance == 0m);
        }
        else if (balanceFilter == "has_activity")
        {
            joined = joined.Where(x => x.TransactionCount > 0);
        }

        if (request.MinBalance.HasValue)
        {
            joined = joined.Where(x => x.Balance >= request.MinBalance.Value);
        }

        if (request.MaxBalance.HasValue)
        {
            joined = joined.Where(x => x.Balance <= request.MaxBalance.Value);
        }

        var sortBy = (request.SortBy ?? "balance_desc").Trim().ToLowerInvariant();
        var ordered = sortBy switch
        {
            "balance_asc" => joined.OrderBy(x => x.Balance).ThenBy(x => x.CustomerName),
            "recent" => joined.OrderByDescending(x => x.LastTransactionAt.HasValue)
                .ThenByDescending(x => x.LastTransactionAt)
                .ThenByDescending(x => x.Balance),
            "spent_desc" => joined.OrderByDescending(x => x.TotalSpent).ThenByDescending(x => x.Balance),
            "name" => joined.OrderBy(x => x.CustomerName),
            _ => joined.OrderByDescending(x => x.Balance)
                .ThenByDescending(x => x.LastTransactionAt)
                .ThenBy(x => x.CustomerName),
        };

        return await ordered
            .Select(x => new AdminWalletListItemDto(
                x.CustomerId,
                x.WalletId,
                x.CustomerName,
                x.PhoneNumber,
                x.IsActive,
                x.MembershipTierName,
                x.Balance,
                x.TotalTopUp,
                x.TotalCashback,
                x.TotalSpent,
                x.TransactionCount,
                x.LastTransactionAt))
            .ToPaginatedListAsync(pagination, cancellationToken);
    }
}
