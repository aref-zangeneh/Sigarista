using Sugarista.Domain.Entities;

namespace Sugarista.Application.Orders.Services;

internal static class SplitBillAmountCalculator
{
    internal static decimal ResolvePayerAmount(Order order, string payerKey)
    {
        return order.SplitBillMode switch
        {
            SplitBillMode.ByPercentage => ResolvePercentageAmount(order, payerKey),
            SplitBillMode.ByCustomAmount => ResolveCustomAmount(order, payerKey),
            _ => order.Lines
                .Where(line => string.Equals(line.PayerKey, payerKey, StringComparison.OrdinalIgnoreCase))
                .Sum(line => line.LineTotal)
        };
    }

    internal static IReadOnlyDictionary<string, decimal> BuildPayerAmounts(Order order)
    {
        var payerKeys = order.SplitShares.Count > 0
            ? order.SplitShares.OrderBy(share => share.SortOrder).Select(share => share.PayerKey).Distinct().ToList()
            : order.Lines
                .Where(line => !string.IsNullOrWhiteSpace(line.PayerKey))
                .Select(line => line.PayerKey!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        var amounts = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        decimal assignedTotal = 0;

        for (var index = 0; index < payerKeys.Count; index++)
        {
            var payerKey = payerKeys[index];
            var isLast = index == payerKeys.Count - 1;

            if (order.SplitBillMode == SplitBillMode.ByPercentage && order.SplitShares.Count > 0)
            {
                var share = order.SplitShares.First(entry => entry.PayerKey == payerKey);
                var amount = isLast
                    ? order.TotalAmount - assignedTotal
                    : RoundMoney(order.TotalAmount * (share.SharePercentage ?? 0m) / 100m);
                amounts[payerKey] = amount;
                assignedTotal += amount;
                continue;
            }

            if (order.SplitBillMode == SplitBillMode.ByCustomAmount && order.SplitShares.Count > 0)
            {
                var share = order.SplitShares.First(entry => entry.PayerKey == payerKey);
                var amount = share.ShareAmount ?? 0m;
                amounts[payerKey] = amount;
                assignedTotal += amount;
                continue;
            }

            amounts[payerKey] = ResolvePayerAmount(order, payerKey);
            assignedTotal += amounts[payerKey];
        }

        return amounts;
    }

    private static decimal ResolvePercentageAmount(Order order, string payerKey)
    {
        var share = order.SplitShares.FirstOrDefault(entry =>
            string.Equals(entry.PayerKey, payerKey, StringComparison.OrdinalIgnoreCase));

        if (share?.SharePercentage is null)
        {
            return 0m;
        }

        var orderedShares = order.SplitShares.OrderBy(entry => entry.SortOrder).ToList();
        var isLast = orderedShares.Last().PayerKey == payerKey;
        if (isLast)
        {
            var previousTotal = orderedShares
                .Where(entry => entry.PayerKey != payerKey)
                .Sum(entry => RoundMoney(order.TotalAmount * (entry.SharePercentage ?? 0m) / 100m));
            return order.TotalAmount - previousTotal;
        }

        return RoundMoney(order.TotalAmount * share.SharePercentage.Value / 100m);
    }

    private static decimal ResolveCustomAmount(Order order, string payerKey)
    {
        return order.SplitShares
            .FirstOrDefault(entry => string.Equals(entry.PayerKey, payerKey, StringComparison.OrdinalIgnoreCase))
            ?.ShareAmount ?? 0m;
    }

    internal static decimal RoundMoney(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
