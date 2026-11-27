using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Extensions;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Common.Models;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Wallets.Queries.GetAdminWalletTransactions;

public record AdminGlobalWalletTransactionDto(
    Guid Id,
    Guid WalletId,
    Guid CustomerId,
    string CustomerName,
    string PhoneNumber,
    WalletTransactionType Type,
    decimal Amount,
    decimal BalanceAfter,
    string Description,
    Guid? OrderId,
    string? OrderTrackingCode,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset CreatedAt);

public record GetAdminWalletTransactionsQuery(
    int Page = 1,
    int PageSize = 30,
    Guid? CustomerId = null,
    WalletTransactionType? Type = null,
    string? Direction = null,
    string? Search = null,
    DateTimeOffset? FromDate = null,
    DateTimeOffset? ToDate = null,
    decimal? MinAmount = null,
    decimal? MaxAmount = null) : IRequest<PaginatedList<AdminGlobalWalletTransactionDto>>;

public class GetAdminWalletTransactionsQueryHandler
    : IRequestHandler<GetAdminWalletTransactionsQuery, PaginatedList<AdminGlobalWalletTransactionDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetAdminWalletTransactionsQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PaginatedList<AdminGlobalWalletTransactionDto>> Handle(
        GetAdminWalletTransactionsQuery request,
        CancellationToken cancellationToken)
    {
        var pagination = new PaginationQuery(request.Page, request.PageSize, request.Search);

        var query = _dbContext.WalletTransactions
            .AsNoTracking()
            .Include(t => t.Wallet)
                .ThenInclude(w => w.Customer)
            .Include(t => t.Order)
            .AsQueryable();

        if (request.CustomerId.HasValue && request.CustomerId.Value != Guid.Empty)
        {
            query = query.Where(t => t.Wallet.CustomerId == request.CustomerId.Value);
        }

        if (request.Type.HasValue)
        {
            query = query.Where(t => t.Type == request.Type.Value);
        }

        var direction = (request.Direction ?? string.Empty).Trim().ToLowerInvariant();
        if (direction == "in")
        {
            query = query.Where(t => t.Amount > 0m);
        }
        else if (direction == "out")
        {
            query = query.Where(t => t.Amount < 0m);
        }

        if (request.FromDate.HasValue)
        {
            query = query.Where(t => t.CreatedAt >= request.FromDate.Value);
        }

        if (request.ToDate.HasValue)
        {
            query = query.Where(t => t.CreatedAt <= request.ToDate.Value);
        }

        if (request.MinAmount.HasValue)
        {
            var minAbs = Math.Abs(request.MinAmount.Value);
            query = query.Where(t => t.Amount >= minAbs || t.Amount <= -minAbs);
        }

        if (request.MaxAmount.HasValue)
        {
            var maxAbs = Math.Abs(request.MaxAmount.Value);
            query = query.Where(t => t.Amount <= maxAbs && t.Amount >= -maxAbs);
        }

        if (!string.IsNullOrWhiteSpace(pagination.Search))
        {
            var search = pagination.Search.Trim().ToLowerInvariant();
            query = query.Where(t =>
                t.Description.ToLower().Contains(search) ||
                t.Wallet.Customer.PersianName.ToLower().Contains(search) ||
                t.Wallet.Customer.PhoneNumber.Contains(search) ||
                (t.Order != null && t.Order.TrackingCode.ToLower().Contains(search)));
        }

        return await query
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new AdminGlobalWalletTransactionDto(
                t.Id,
                t.WalletId,
                t.Wallet.CustomerId,
                t.Wallet.Customer.PersianName,
                t.Wallet.Customer.PhoneNumber,
                t.Type,
                t.Amount,
                t.BalanceAfter,
                t.Description,
                t.OrderId,
                t.Order != null ? t.Order.TrackingCode : null,
                t.ExpiresAt,
                t.CreatedAt))
            .ToPaginatedListAsync(pagination, cancellationToken);
    }
}
