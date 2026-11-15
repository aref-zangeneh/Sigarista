namespace Sugarista.Application.Sms;

/// <summary>Shared paged-result shape for every SMS list endpoint.</summary>
public sealed record SmsPagedResult<T>(
    IReadOnlyList<T> Items,
    int Total,
    int Page,
    int PageSize);

public static class SmsPagination
{
    public static (int Page, int PageSize) Normalize(int page, int pageSize) =>
        (Math.Max(1, page), Math.Clamp(pageSize, 1, 200));
}
