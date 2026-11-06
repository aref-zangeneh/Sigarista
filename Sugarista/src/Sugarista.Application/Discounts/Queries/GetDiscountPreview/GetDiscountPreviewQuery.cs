using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Customers.Services;
using Sugarista.Application.Discounts.Engine;
using Sugarista.Application.Discounts.Security;
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
    string TimeZoneId = "Asia/Tehran",
    string? PhoneNumber = null,
    string? IpAddress = null) : IRequest<DiscountEvaluationResult>;

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

        var settings = await _dbContext.SiteSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        var normalizedCode = Discount.NormalizeCode(request.Code);

        // Abuse protection is enforced here too, so a locked or blacklisted
        // customer gets the clear Persian message before submitting an order.
        if (normalizedCode is not null)
        {
            await DiscountAbuseGuard.EnsureAllowedAsync(
                _dbContext,
                settings,
                request.PhoneNumber,
                request.IpAddress,
                cancellationToken);
        }

        var result = await _discountEngine.EvaluateAsync(
            new DiscountEvaluationContext(
                lines,
                request.DeliveryFee,
                normalizedCode,
                request.CustomerId,
                request.CustomerGroups,
                request.Now ?? DateTimeOffset.UtcNow,
                request.Channel,
                request.IsOnlinePayment,
                request.TimeZoneId),
            cancellationToken);

        if (normalizedCode is not null)
        {
            if (result.Applications.Count == 0 && result.Rejections.Count > 0)
            {
                await DiscountAbuseGuard.RecordFailureAsync(
                    _dbContext,
                    settings,
                    request.PhoneNumber,
                    normalizedCode,
                    request.IpAddress,
                    cancellationToken);
            }
            else if (result.Applications.Count > 0)
            {
                await DiscountAbuseGuard.RecordSuccessAsync(
                    _dbContext,
                    settings,
                    request.PhoneNumber,
                    normalizedCode,
                    request.IpAddress,
                    cancellationToken);
            }
        }

        // Show the capped amount in the cart exactly as checkout will charge it.
        var customerId = request.CustomerId;
        if (customerId is null && !string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            var phone = CustomerDirectoryService.NormalizePhone(request.PhoneNumber);
            customerId = await _dbContext.Customers
                .AsNoTracking()
                .Where(customer => customer.PhoneNumber == phone)
                .Select(customer => (Guid?)customer.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var remainingCap = await DiscountDailyCapService.GetRemainingAllowanceAsync(
            _dbContext,
            settings,
            customerId,
            request.TimeZoneId,
            request.Now ?? DateTimeOffset.UtcNow,
            cancellationToken: cancellationToken);

        if (remainingCap is { } allowance)
        {
            var adjustedApplications = new List<DiscountApplication>();
            var left = allowance;

            foreach (var application in result.Applications)
            {
                if (left <= 0)
                {
                    break;
                }

                var amount = Math.Min(application.Amount, left);
                if (amount <= 0)
                {
                    continue;
                }

                adjustedApplications.Add(application with { Amount = amount });
                left -= amount;
            }

            result = result with
            {
                Applications = adjustedApplications,
                DiscountTotal = adjustedApplications.Sum(application => application.Amount)
            };
        }

        return result;
    }
}
