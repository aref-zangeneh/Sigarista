using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Extensions;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Common.Models;
using Sugarista.Domain.Constants;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Orders.Queries.GetAdminOrders;

public record GetAdminOrdersQuery(
    int Page = 1,
    int PageSize = 25,
    string? Search = null,
    string? Status = null,
    string? Fulfillment = null,
    string? OrderKind = null,
    bool? ExcludedFromAccounting = null,
    string? Channel = null,
    string? ServiceType = null,
    string? PaymentMethod = null,
    string? FromDate = null,
    string? ToDate = null,
    decimal? MinAmount = null,
    decimal? MaxAmount = null,
    bool? HasDiscount = null,
    bool? IsSplitBill = null,
    bool? HasNote = null,
    bool? HasManagerNote = null,
    string? MenuItemQuery = null,
    string? TableQuery = null,
    Guid? DeliveryMethodId = null,
    string? SortBy = null) : IRequest<PaginatedList<AdminOrderListItemDto>>;

public record AdminOrderListItemDto(
    Guid Id,
    string TrackingCode,
    string CustomerName,
    string CustomerPhone,
    decimal TotalAmount,
    decimal AmountPaid,
    bool IsSplitBill,
    int? GuestCount,
    string Status,
    string FulfillmentStatus,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PaidAt,
    DateTimeOffset? FulfillmentUpdatedAt,
    DateTimeOffset? ReadyAt,
    DateTimeOffset? ReadySmsSentAt,
    int LineCount,
    string OrderKind = "Standard",
    string? ComplimentaryReason = null,
    bool IsExcludedFromAccounting = false,
    string? PaymentMethod = null,
    decimal DiscountTotal = 0m,
    string? ManualDiscountType = null,
    decimal? ManualDiscountValue = null,
    decimal ManualDiscountAmount = 0m,
    string? ManualDiscountReason = null,
    string? ManualDiscountAppliedBy = null,
    string? ManagerNote = null);

public class GetAdminOrdersQueryHandler : IRequestHandler<GetAdminOrdersQuery, PaginatedList<AdminOrderListItemDto>>
{
    private static readonly TimeSpan TehranOffset = TimeSpan.FromHours(3.5);
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public GetAdminOrdersQueryHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<PaginatedList<AdminOrderListItemDto>> Handle(GetAdminOrdersQuery request, CancellationToken cancellationToken)
    {
        var pagination = new PaginationQuery(request.Page, request.PageSize, request.Search);
        var includeManagerNote = _currentUserService.HasPermission(AdminPermissions.Pos.ManagerNote);

        var query = _dbContext.Orders.AsNoTracking();

        // 1. Omni-search across EVERY relevant order field
        if (!string.IsNullOrWhiteSpace(pagination.Search))
        {
            var search = pagination.Search.Trim().ToLowerInvariant();
            query = query.Where(order =>
                order.TrackingCode.ToLower().Contains(search) ||
                order.CustomerName.ToLower().Contains(search) ||
                order.CustomerPhone.ToLower().Contains(search) ||
                (order.Note != null && order.Note.ToLower().Contains(search)) ||
                (order.ComplimentaryReason != null && order.ComplimentaryReason.ToLower().Contains(search)) ||
                (order.ManualDiscountReason != null && order.ManualDiscountReason.ToLower().Contains(search)) ||
                (includeManagerNote && order.ManagerNote != null && order.ManagerNote.ToLower().Contains(search)) ||
                (order.SalonTable != null && order.SalonTable.Name.ToLower().Contains(search)) ||
                (order.DeliveryMethod != null && (order.DeliveryMethod.PersianName.ToLower().Contains(search) || order.DeliveryMethod.EnglishName.ToLower().Contains(search))) ||
                order.Lines.Any(line =>
                    line.PersianName.ToLower().Contains(search) ||
                    line.EnglishName.ToLower().Contains(search) ||
                    (line.BaristaNote != null && line.BaristaNote.ToLower().Contains(search))) ||
                order.Payments.Any(payment =>
                    (payment.ReferenceId != null && payment.ReferenceId.ToLower().Contains(search)) ||
                    payment.PaymentAuthority.ToLower().Contains(search)));
        }

        // 2. Payment / Lifecycle Status
        if (!string.IsNullOrWhiteSpace(request.Status) &&
            Enum.TryParse<OrderStatus>(request.Status, true, out var statusFilter))
        {
            query = query.Where(order => order.Status == statusFilter);
        }

        // 3. Fulfillment Status
        if (!string.IsNullOrWhiteSpace(request.Fulfillment) &&
            Enum.TryParse<FulfillmentStatus>(request.Fulfillment, true, out var fulfillmentFilter))
        {
            query = query.Where(order => order.FulfillmentStatus == fulfillmentFilter);
        }

        // 4. Order Kind (Standard / InternalUse / Complimentary)
        if (!string.IsNullOrWhiteSpace(request.OrderKind) &&
            Enum.TryParse<OrderKind>(request.OrderKind, true, out var orderKindFilter))
        {
            query = query.Where(order => order.OrderKind == orderKindFilter);
        }

        // 5. Accounting Exclusion
        if (request.ExcludedFromAccounting.HasValue)
        {
            query = query.Where(order => order.IsExcludedFromAccounting == request.ExcludedFromAccounting.Value);
        }

        // 6. Order Channel (Online / Pos)
        if (!string.IsNullOrWhiteSpace(request.Channel) &&
            Enum.TryParse<OrderChannel>(request.Channel, true, out var channelFilter))
        {
            query = query.Where(order => order.Channel == channelFilter);
        }

        // 7. Service Type (DineIn / TakeAway)
        if (!string.IsNullOrWhiteSpace(request.ServiceType) &&
            Enum.TryParse<PosServiceType>(request.ServiceType, true, out var serviceTypeFilter))
        {
            query = query.Where(order => order.ServiceType == serviceTypeFilter);
        }

        // 8. Payment Method (matches top-level method OR any paid leg in Split payments)
        if (!string.IsNullOrWhiteSpace(request.PaymentMethod) &&
            Enum.TryParse<PosPaymentMethod>(request.PaymentMethod, true, out var paymentMethodFilter))
        {
            query = query.Where(order =>
                order.PaymentMethod == paymentMethodFilter ||
                order.Payments.Any(p => p.PaymentMethod == paymentMethodFilter));
        }

        // 9. Tehran Calendar Date Range (FromDate / ToDate)
        if (TryParseTehranStartUtc(request.FromDate, out var fromUtc))
        {
            query = query.Where(order => order.CreatedAt >= fromUtc);
        }

        if (TryParseTehranEndExclusiveUtc(request.ToDate, out var toExclusiveUtc))
        {
            query = query.Where(order => order.CreatedAt < toExclusiveUtc);
        }

        // 10. Amount Range
        if (request.MinAmount.HasValue)
        {
            query = query.Where(order => order.TotalAmount >= request.MinAmount.Value);
        }

        if (request.MaxAmount.HasValue)
        {
            query = query.Where(order => order.TotalAmount <= request.MaxAmount.Value);
        }

        // 11. Discount / Split / Notes flags
        if (request.HasDiscount.HasValue)
        {
            query = request.HasDiscount.Value
                ? query.Where(order => order.DiscountTotal > 0m || order.ManualDiscountAmount > 0m)
                : query.Where(order => order.DiscountTotal <= 0m && order.ManualDiscountAmount <= 0m);
        }

        if (request.IsSplitBill.HasValue)
        {
            query = query.Where(order => order.IsSplitBill == request.IsSplitBill.Value);
        }

        if (request.HasNote.HasValue)
        {
            query = request.HasNote.Value
                ? query.Where(order => order.Note != null && order.Note != "")
                : query.Where(order => order.Note == null || order.Note == "");
        }

        if (includeManagerNote && request.HasManagerNote.HasValue)
        {
            query = request.HasManagerNote.Value
                ? query.Where(order => order.ManagerNote != null && order.ManagerNote != "")
                : query.Where(order => order.ManagerNote == null || order.ManagerNote == "");
        }

        // 12. Specific Menu Item inside order
        if (!string.IsNullOrWhiteSpace(request.MenuItemQuery))
        {
            var itemTerm = request.MenuItemQuery.Trim().ToLowerInvariant();
            query = query.Where(order => order.Lines.Any(line =>
                line.PersianName.ToLower().Contains(itemTerm) ||
                line.EnglishName.ToLower().Contains(itemTerm)));
        }

        // 13. Specific Salon Table name/code
        if (!string.IsNullOrWhiteSpace(request.TableQuery))
        {
            var tableTerm = request.TableQuery.Trim().ToLowerInvariant();
            query = query.Where(order =>
                order.SalonTable != null &&
                order.SalonTable.Name.ToLower().Contains(tableTerm));
        }

        // 14. Delivery Method
        if (request.DeliveryMethodId.HasValue)
        {
            query = query.Where(order => order.DeliveryMethodId == request.DeliveryMethodId.Value);
        }

        // 15. Sorting
        var ordered = (request.SortBy ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "oldest" => query.OrderBy(o => o.CreatedAt),
            "amount_desc" => query.OrderByDescending(o => o.TotalAmount).ThenByDescending(o => o.CreatedAt),
            "amount_asc" => query.OrderBy(o => o.TotalAmount).ThenByDescending(o => o.CreatedAt),
            _ => query.OrderByDescending(o => o.CreatedAt)
        };

        return await ordered
            .Select(order => new AdminOrderListItemDto(
                order.Id,
                order.TrackingCode,
                order.CustomerName,
                order.CustomerPhone,
                order.TotalAmount,
                order.AmountPaid,
                order.IsSplitBill,
                order.GuestCount,
                order.Status.ToString(),
                order.FulfillmentStatus.ToString(),
                order.CreatedAt,
                order.PaidAt,
                order.FulfillmentUpdatedAt,
                order.ReadyAt,
                order.ReadySmsSentAt,
                order.Lines.Count,
                order.OrderKind.ToString(),
                order.ComplimentaryReason,
                order.IsExcludedFromAccounting,
                order.PaymentMethod != null ? order.PaymentMethod.ToString() : null,
                order.DiscountTotal,
                order.ManualDiscountType != null ? order.ManualDiscountType.ToString() : null,
                order.ManualDiscountValue,
                order.ManualDiscountAmount,
                order.ManualDiscountReason,
                order.ManualDiscountAppliedBy,
                includeManagerNote ? order.ManagerNote : null))
            .ToPaginatedListAsync(pagination, cancellationToken);
    }

    private static bool TryParseTehranStartUtc(string? input, out DateTimeOffset utc)
    {
        utc = default;
        if (string.IsNullOrWhiteSpace(input)) return false;
        var trimmed = input.Trim();
        if (DateOnly.TryParseExact(trimmed, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
        {
            utc = new DateTimeOffset(d.Year, d.Month, d.Day, 0, 0, 0, TehranOffset).ToUniversalTime();
            return true;
        }
        if (DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            utc = parsed.ToUniversalTime();
            return true;
        }
        return false;
    }

    private static bool TryParseTehranEndExclusiveUtc(string? input, out DateTimeOffset utc)
    {
        utc = default;
        if (string.IsNullOrWhiteSpace(input)) return false;
        var trimmed = input.Trim();
        if (DateOnly.TryParseExact(trimmed, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
        {
            var nextDay = d.AddDays(1);
            utc = new DateTimeOffset(nextDay.Year, nextDay.Month, nextDay.Day, 0, 0, 0, TehranOffset).ToUniversalTime();
            return true;
        }
        if (DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            utc = parsed.ToUniversalTime();
            return true;
        }
        return false;
    }
}
