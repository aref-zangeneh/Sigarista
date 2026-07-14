using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Models;

namespace Sugarista.Application.Common.Extensions;

public static class QueryablePaginationExtensions
{
    public static async Task<PaginatedList<T>> ToPaginatedListAsync<T>(
        this IQueryable<T> query,
        PaginationQuery pagination,
        CancellationToken cancellationToken = default)
    {
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip(pagination.Skip)
            .Take(pagination.NormalizedPageSize)
            .ToListAsync(cancellationToken);

        return new PaginatedList<T>(
            items,
            totalCount,
            pagination.NormalizedPage,
            pagination.NormalizedPageSize);
    }
}
