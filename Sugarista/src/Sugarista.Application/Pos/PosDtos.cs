using Sugarista.Application.Menu.Queries.GetPublicMenu;

namespace Sugarista.Application.Pos;

public record PosBootstrapDto(
    PublicMenuDto Menu,
    IReadOnlyCollection<PosTableDto> Tables,
    IReadOnlyCollection<PosOrderSummaryDto> HeldOrders,
    IReadOnlyCollection<PosOrderSummaryDto> RecentOrders);

public record PosTableDto(
    Guid Id,
    string Name,
    int Capacity,
    int DisplayOrder,
    bool IsActive,
    bool IsOccupied,
    Guid? ActiveSessionId,
    int PeopleCount,
    DateTimeOffset? StartedAt,
    int DurationMinutes,
    string? Note,
    decimal CurrentBalance);

public record PosOrderSummaryDto(
    Guid Id,
    string TrackingCode,
    string CustomerName,
    string? ServiceType,
    string Status,
    decimal TotalAmount,
    decimal AmountPaid,
    int LineCount,
    Guid? SalonTableId,
    string? TableName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? HeldAt);

public record PosOrderDetailDto(
    Guid Id,
    string TrackingCode,
    string CustomerName,
    string CustomerPhone,
    string? Note,
    string Status,
    string? ServiceType,
    string? PaymentMethod,
    decimal TotalAmount,
    decimal AmountPaid,
    bool IsSplitBill,
    string? SplitBillMode,
    int? GuestCount,
    Guid? SalonTableId,
    string? TableName,
    Guid? TableSessionId,
    Guid? DeliveryMethodId,
    string? DeliveryMethodName,
    decimal DeliveryFee,
    DateTimeOffset CreatedAt,
    DateTimeOffset? HeldAt,
    IReadOnlyCollection<PosOrderLineDto> Lines,
    IReadOnlyCollection<PosSplitShareDto> SplitShares);

public record PosOrderLineDto(
    Guid Id,
    Guid MenuItemId,
    string PersianName,
    string EnglishName,
    string? SizeKey,
    string? SizeLabel,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    string? BaristaNote,
    string? PayerKey,
    string? PayerLabel,
    IReadOnlyCollection<string> ModifierLabels,
    IReadOnlyCollection<Guid> ModifierIds);

public record PosSplitShareDto(
    string PayerKey,
    string PayerLabel,
    decimal? SharePercentage,
    decimal? ShareAmount);

public record UpsertPosOrderResultDto(
    Guid OrderId,
    string TrackingCode,
    string Status,
    decimal TotalAmount,
    string? PaymentMethod);
