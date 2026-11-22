using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Warehouses.Dto;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Warehouses.Queries.GetWarehouseVouchers;

public record GetWarehouseVouchersQuery(
    string? Type = null,
    string? Status = null,
    Guid? WarehouseId = null,
    bool MainOnly = false,
    string? Reason = null,
    string? Search = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Page = 1,
    int PageSize = 30) : IRequest<WarehouseVoucherListResponseDto>;

public class GetWarehouseVouchersQueryHandler
    : IRequestHandler<GetWarehouseVouchersQuery, WarehouseVoucherListResponseDto>
{
    private readonly IApplicationDbContext _dbContext;

    public GetWarehouseVouchersQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<WarehouseVoucherListResponseDto> Handle(
        GetWarehouseVouchersQuery request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        // Base scope for summary KPIs (respects warehouse + date filters so summary matches user context)
        var scoped = _dbContext.WarehouseVouchers.AsNoTracking();

        if (request.MainOnly)
        {
            scoped = scoped.Where(v => v.WarehouseId == null);
        }
        else if (request.WarehouseId.HasValue)
        {
            scoped = scoped.Where(v => v.WarehouseId == request.WarehouseId.Value);
        }

        if (request.From.HasValue)
        {
            scoped = scoped.Where(v => v.VoucherDate >= request.From.Value);
        }

        if (request.To.HasValue)
        {
            scoped = scoped.Where(v => v.VoucherDate <= request.To.Value);
        }

        var summaryRows = await scoped
            .GroupBy(v => new { v.VoucherType, v.Status })
            .Select(g => new
            {
                g.Key.VoucherType,
                g.Key.Status,
                Count = g.Count(),
                Amount = g.Sum(x => x.TotalAmount),
            })
            .ToListAsync(cancellationToken);

        var summary = new WarehouseVoucherSummaryDto(
            PostedReceiptsCount: summaryRows
                .Where(r => r.VoucherType == WarehouseVoucherType.Receipt && r.Status == WarehouseVoucherStatus.Posted)
                .Sum(r => r.Count),
            PostedReceiptsAmount: summaryRows
                .Where(r => r.VoucherType == WarehouseVoucherType.Receipt && r.Status == WarehouseVoucherStatus.Posted)
                .Sum(r => r.Amount),
            PostedIssuesCount: summaryRows
                .Where(r => r.VoucherType == WarehouseVoucherType.Issue && r.Status == WarehouseVoucherStatus.Posted)
                .Sum(r => r.Count),
            PostedIssuesAmount: summaryRows
                .Where(r => r.VoucherType == WarehouseVoucherType.Issue && r.Status == WarehouseVoucherStatus.Posted)
                .Sum(r => r.Amount),
            CancelledCount: summaryRows
                .Where(r => r.Status == WarehouseVoucherStatus.Cancelled)
                .Sum(r => r.Count));

        var query = scoped;

        if (!string.IsNullOrWhiteSpace(request.Type) &&
            Enum.TryParse<WarehouseVoucherType>(request.Type, true, out var parsedType))
        {
            query = query.Where(v => v.VoucherType == parsedType);
        }

        if (!string.IsNullOrWhiteSpace(request.Status) &&
            Enum.TryParse<WarehouseVoucherStatus>(request.Status, true, out var parsedStatus))
        {
            query = query.Where(v => v.Status == parsedStatus);
        }

        if (!string.IsNullOrWhiteSpace(request.Reason) &&
            Enum.TryParse<WarehouseVoucherReason>(request.Reason, true, out var parsedReason))
        {
            query = query.Where(v => v.Reason == parsedReason);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLowerInvariant();
            query = query.Where(v =>
                v.VoucherNumber.ToLower().Contains(term) ||
                (v.CounterpartyName != null && v.CounterpartyName.ToLower().Contains(term)) ||
                (v.ExternalReference != null && v.ExternalReference.ToLower().Contains(term)) ||
                (v.Note != null && v.Note.ToLower().Contains(term)) ||
                (v.Supplier != null && v.Supplier.PersianName.ToLower().Contains(term)) ||
                v.Lines.Any(l =>
                    l.Ingredient.PersianName.ToLower().Contains(term) ||
                    l.Ingredient.EnglishName.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Include(v => v.Warehouse)
            .Include(v => v.Supplier)
            .Include(v => v.Lines)
                .ThenInclude(l => l.Ingredient)
            .OrderByDescending(v => v.VoucherDate)
            .ThenByDescending(v => v.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new WarehouseVoucherListResponseDto(
            summary,
            items.Select(WarehouseVoucherMapper.ToDto).ToArray(),
            totalCount,
            page,
            pageSize);
    }
}
