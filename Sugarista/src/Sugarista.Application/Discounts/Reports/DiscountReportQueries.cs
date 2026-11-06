using Sugarista.Application.Common.Interfaces;

namespace Sugarista.Application.Discounts.Reports;

/// <summary>On-screen preview of the discount performance report.</summary>
public record GetDiscountReportQuery(
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    Guid? DiscountId = null,
    Guid? CategoryId = null,
    bool OnlyUnscopedCategories = false,
    string? Kind = null,
    string? Channel = null,
    string? Code = null,
    IReadOnlyCollection<string>? Columns = null) : IRequest<DiscountReportResult>;

public class GetDiscountReportQueryHandler : IRequestHandler<GetDiscountReportQuery, DiscountReportResult>
{
    private readonly DiscountReportBuilder _builder;

    public GetDiscountReportQueryHandler(DiscountReportBuilder builder)
    {
        _builder = builder;
    }

    public Task<DiscountReportResult> Handle(
        GetDiscountReportQuery request,
        CancellationToken cancellationToken) =>
        _builder.BuildAsync(
            DiscountReportPeriod.Normalize(
                request.From,
                request.To,
                request.DiscountId,
                request.CategoryId,
                request.OnlyUnscopedCategories,
                request.Kind,
                request.Channel,
                request.Code,
                request.Columns),
            cancellationToken);
}

/// <summary>Synchronous Excel download of the discount performance report.</summary>
public record GetDiscountReportExcelQuery(
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    Guid? DiscountId = null,
    Guid? CategoryId = null,
    bool OnlyUnscopedCategories = false,
    string? Kind = null,
    string? Channel = null,
    string? Code = null,
    IReadOnlyCollection<string>? Columns = null) : IRequest<ExcelWorkbookFile>;

public class GetDiscountReportExcelQueryHandler : IRequestHandler<GetDiscountReportExcelQuery, ExcelWorkbookFile>
{
    private readonly DiscountReportBuilder _builder;

    public GetDiscountReportExcelQueryHandler(DiscountReportBuilder builder)
    {
        _builder = builder;
    }

    public Task<ExcelWorkbookFile> Handle(
        GetDiscountReportExcelQuery request,
        CancellationToken cancellationToken) =>
        _builder.BuildExcelAsync(
            DiscountReportPeriod.Normalize(
                request.From,
                request.To,
                request.DiscountId,
                request.CategoryId,
                request.OnlyUnscopedCategories,
                request.Kind,
                request.Channel,
                request.Code,
                request.Columns),
            cancellationToken);
}

/// <summary>Shared period/filter normalization (defaults to the last 30 days).</summary>
public static class DiscountReportPeriod
{
    public static DiscountReportFilter Normalize(
        DateTimeOffset? from,
        DateTimeOffset? to,
        Guid? discountId,
        Guid? categoryId,
        bool onlyUnscopedCategories,
        string? kind,
        string? channel,
        string? code,
        IReadOnlyCollection<string>? columns)
    {
        var end = to ?? DateTimeOffset.UtcNow;
        var start = from ?? end.AddDays(-30);

        if (start > end)
        {
            (start, end) = (end, start);
        }

        return new DiscountReportFilter(
            start,
            end,
            discountId,
            categoryId,
            onlyUnscopedCategories,
            kind,
            channel,
            code,
            columns);
    }
}
