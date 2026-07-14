using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Orders.Services;

namespace Sugarista.Application.Orders.Queries.GetSplitOrderStatus;

public record GetSplitOrderStatusQuery(Guid OrderId) : IRequest<SplitOrderStatusDto?>;

public record SplitOrderStatusDto(
    Guid Id,
    string TrackingCode,
    string CustomerName,
    string Status,
    decimal TotalAmount,
    decimal AmountPaid,
    int GuestCount,
    IReadOnlyCollection<SplitOrderPayerDto> Payers,
    IReadOnlyCollection<SplitOrderLineDto> Lines);

public record SplitOrderPayerDto(
    string PayerKey,
    string PayerLabel,
    decimal Amount,
    bool IsPaid,
    DateTimeOffset? PaidAt);

public record SplitOrderLineDto(
    string PersianName,
    string EnglishName,
    string? SizeLabel,
    int Quantity,
    decimal LineTotal,
    string PayerKey,
    string PayerLabel,
    IReadOnlyCollection<string> ModifierLabels);

public class GetSplitOrderStatusQueryHandler : IRequestHandler<GetSplitOrderStatusQuery, SplitOrderStatusDto?>
{
    private readonly IApplicationDbContext _dbContext;

    public GetSplitOrderStatusQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SplitOrderStatusDto?> Handle(GetSplitOrderStatusQuery request, CancellationToken cancellationToken)
    {
        var order = await _dbContext.Orders
            .AsNoTracking()
            .Include(o => o.Lines)
            .Include(o => o.Payments)
            .Include(o => o.SplitShares)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);

        if (order is null || !order.IsSplitBill)
        {
            return null;
        }

        var payerAmounts = SplitBillAmountCalculator.BuildPayerAmounts(order);

        var payers = payerAmounts
            .Select(entry =>
            {
                var payerKey = entry.Key;
                var amount = entry.Value;
                var label = order.SplitShares
                        .FirstOrDefault(share => string.Equals(share.PayerKey, payerKey, StringComparison.OrdinalIgnoreCase))
                        ?.PayerLabel
                    ?? order.Lines
                        .FirstOrDefault(line => string.Equals(line.PayerKey, payerKey, StringComparison.OrdinalIgnoreCase))
                        ?.PayerLabel
                    ?? payerKey;

                var payment = order.Payments
                    .Where(p => p.PayerKey == payerKey && p.Status == Domain.Entities.OrderPaymentStatus.Paid)
                    .OrderByDescending(p => p.PaidAt)
                    .FirstOrDefault();

                return new SplitOrderPayerDto(
                    payerKey,
                    label,
                    amount,
                    payment is not null,
                    payment?.PaidAt);
            })
            .OrderBy(payer => payer.PayerKey)
            .ToArray();

        return new SplitOrderStatusDto(
            order.Id,
            order.TrackingCode,
            order.CustomerName,
            order.Status.ToString(),
            order.TotalAmount,
            order.AmountPaid,
            order.GuestCount ?? payers.Length,
            payers,
            order.Lines
                .Select(line => new SplitOrderLineDto(
                    line.PersianName,
                    line.EnglishName,
                    line.SizeLabel,
                    line.Quantity,
                    line.LineTotal,
                    line.PayerKey ?? string.Empty,
                    line.PayerLabel ?? string.Empty,
                    line.ModifierLabels))
                .ToArray());
    }
}
