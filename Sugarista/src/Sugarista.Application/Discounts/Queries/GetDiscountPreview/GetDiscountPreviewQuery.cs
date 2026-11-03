using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Discounts.Engine;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Discounts.Queries.GetDiscountPreview;

public record GetDiscountPreviewQuery(
    string? Code,
    IReadOnlyCollection<DiscountLineInput> Lines,
    decimal DeliveryFee = 0m,
    Guid? CustomerId = null,
    IReadOnlyCollection<DiscountCustomerGroup>? CustomerGroups = null,
    DateTimeOffset? Now = null,
    string Channel = "online",
    bool IsOnlinePayment = false,
    string TimeZoneId = "Asia/Tehran") : IRequest<DiscountEvaluationResult>;

/// <summary>
/// Evaluates the discounts a cart would receive — without creating an order.
/// Used by the storefront to show the discount line and the final amount in the
/// cart and on the payment screen, and to surface a clear rejection message for
/// an unknown/expired code before the customer submits the order.
/// </summary>
public class GetDiscountPreviewQueryHandler : IRequestHandler<GetDiscountPreviewQuery, DiscountEvaluationResult>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IDiscountEngine _discountEngine;

    public GetDiscountPreviewQueryHandler(IApplicationDbContext dbContext, IDiscountEngine discountEngine)
    {
        _dbContext = dbContext;
        _discountEngine = discountEngine;
    }

    public async Task<DiscountEvaluationResult> Handle(
        GetDiscountPreviewQuery request,
        CancellationToken cancellationToken)
    {
        // Callers (the storefront) only know menu item ids, so resolve the
        // category of every line that did not provide one. Category-scoped
        // discounts then preview exactly the way checkout evaluates them.
        var lines = request.Lines;

        if (lines.Any(line => line.CategoryId is null))
        {
            var unresolvedItemIds = lines
                .Where(line => line.CategoryId is null)
                .Select(line => line.MenuItemId)
                .Distinct()
                .ToList();

            var categoryByItemId = await _dbContext.MenuItems
                .AsNoTracking()
                .Where(item => unresolvedItemIds.Contains(item.Id))
                .Select(item => new { item.Id, item.CategoryId })
                .ToDictionaryAsync(item => item.Id, item => item.CategoryId, cancellationToken);

            lines = lines
                .Select(line => line.CategoryId is null && categoryByItemId.TryGetValue(line.MenuItemId, out var categoryId)
                    ? line with { CategoryId = categoryId }
                    : line)
                .ToList();
        }

        return await _discountEngine.EvaluateAsync(
            new DiscountEvaluationContext(
                lines,
                request.DeliveryFee,
                Discount.NormalizeCode(request.Code),
                request.CustomerId,
                request.CustomerGroups,
                request.Now ?? DateTimeOffset.UtcNow,
                request.Channel,
                request.IsOnlinePayment,
                request.TimeZoneId),
            cancellationToken);
    }
}
