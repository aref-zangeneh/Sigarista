namespace Sugarista.Application.Common.Models;

public record PaginatedList<T>(
    IReadOnlyCollection<T> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPreviousPage => Page > 1;

    public bool HasNextPage => Page < TotalPages;
}

public record PaginationQuery(
    int Page = 1,
    int PageSize = 25,
    string? Search = null,
    string? SortBy = null,
    string? SortDirection = null)
{
    public int NormalizedPage => Page < 1 ? 1 : Page;

    public int NormalizedPageSize => PageSize switch
    {
        7 => 7,
        25 => 25,
        50 => 50,
        100 => 100,
        _ => 25
    };

    public int Skip => (NormalizedPage - 1) * NormalizedPageSize;
}
