using Sugarista.Domain.Entities;

namespace Sugarista.Application.Discounts.Engine.Evaluators;

/// <summary>
/// Buy-X-Get-Y: for every <c>BuyQuantity + GetQuantity</c> eligible items the
/// customer takes, the cheapest (or most expensive, if configured) eligible
/// items are given away for free. The free units are always the lowest-priced
/// ones by default, so the offer never over-discounts an expensive item.
/// </summary>
public sealed class BuyXGetYDiscountEvaluator : IDiscountEvaluator
{
    public DiscountType Type => DiscountType.BuyXGetY;

    public DiscountComputation? Evaluate(
        Discount discount,
        DiscountEvaluationContext context,
        decimal eligibleBase,
        IReadOnlyList<DiscountLineInput> eligibleLines)
    {
        if (eligibleLines.Count == 0)
        {
            return null;
        }

        var buy = Math.Max(1, discount.BuyQuantity);
        var get = Math.Max(1, discount.GetQuantity);
        var cycle = buy + get;

        var totalQuantity = eligibleLines.Sum(line => line.Quantity);
        if (totalQuantity < cycle)
        {
            return null;
        }

        var applications = totalQuantity / cycle;
        if (discount.MaxApplications is { } cap && applications > cap)
        {
            applications = cap;
        }

        var freeUnits = applications * get;
        if (freeUnits <= 0)
        {
            return null;
        }

        var units = new List<(DiscountLineInput Line, decimal Price)>();
        foreach (var line in eligibleLines)
        {
            for (var i = 0; i < line.Quantity; i++)
            {
                units.Add((line, line.UnitPrice));
            }
        }

        units = discount.FreeItemSelection == FreeItemSelection.MostExpensive
            ? units.OrderByDescending(unit => unit.Price)
                .ThenBy(unit => unit.Line.MenuItemId)
                .ToList()
            : units.OrderBy(unit => unit.Price)
                .ThenBy(unit => unit.Line.MenuItemId)
                .ToList();

        var selected = units.Take(freeUnits).ToList();
        if (selected.Count == 0)
        {
            return null;
        }

        var shares = selected
            .GroupBy(unit => (unit.Line.MenuItemId, unit.Line.SizeKey, unit.Line.PayerKey))
            .Select(group => new DiscountLineShare(
                group.Key.MenuItemId,
                group.Key.SizeKey,
                group.Key.PayerKey,
                group.Sum(unit => unit.Price)))
            .ToList();

        var amount = shares.Sum(share => share.Amount);
        if (amount <= 0)
        {
            return null;
        }

        return new DiscountComputation(
            amount,
            shares,
            $"{discount.Name} — خرید {buy} بگیر {get}");
    }
}
