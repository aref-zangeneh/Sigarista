using Sugarista.Application.Affiliates.Services;
using Sugarista.Domain.Constants;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.UnitTests.Affiliates;

public class AffiliateCommissionSnapshotTests
{
    [Fact]
    public void CommissionSnapshot_LocksPricesAndRatesImmutablyAgainstFutureChanges()
    {
        var drinksCategoryId = Guid.NewGuid();
        var cakesCategoryId = Guid.NewGuid();
        var latteId = Guid.NewGuid();
        var cakeId = Guid.NewGuid();

        var tier = new AffiliateTier("نقره‌ای", 10_000_000m, 8m, 15m);
        var affiliate = new Affiliate(Guid.NewGuid(), tierId: tier.Id, status: AffiliateStatus.Active);

        var order = new Order("SG-9001", "سارا", "09123334455", null, 400_000m, "AUTH-1");
        order.AddLine(new OrderLine(order.Id, latteId, "لاته", "Latte", "medium", "متوسط", 2, 120_000m, [])); // 240,000
        order.AddLine(new OrderLine(order.Id, cakeId, "چیزکیک", "Cheesecake", "slice", "اسلایس", 1, 160_000m, [])); // 160,000

        var lines = new List<AffiliateCommissionLineInput>
        {
            new(latteId, drinksCategoryId, 240_000m),
            new(cakeId, cakesCategoryId, 160_000m),
        };

        // Rates at order time: Drinks = 12%, Cakes = 5%
        var categoryRatesAtOrder = new Dictionary<Guid, decimal>
        {
            [drinksCategoryId] = 12m,
            [cakesCategoryId] = 5m,
        };

        var categoryNames = new Dictionary<Guid, string>
        {
            [drinksCategoryId] = "نوشیدنی گرم",
            [cakesCategoryId] = "کیک و دسر",
        };

        var calc = AffiliateCommissionCalculator.CalculateOrderCommission(
            affiliate,
            tier,
            lines,
            totalDiscountAmount: 0m,
            categoryRatesAtOrder);

        var now = new DateTimeOffset(2026, 11, 27, 18, 0, 0, TimeSpan.Zero);
        var snapshotJson = AffiliateCommissionSnapshot
            .Build(order, affiliate, calc, categoryNames, now)
            .ToJson();

        var commission = new AffiliateCommission(
            affiliate.Id,
            order.Id,
            AffiliateOrderSource.Online,
            order.TotalAmount,
            calc.EligibleAmount,
            calc.CommissionAmount,
            AffiliateCommissionStatus.Pending,
            snapshotJson);

        // Drinks: 240,000 * 12% = 28,800; Cake: 160,000 * 5% = 8,000 => 36,800 locked!
        Assert.Equal(36_800m, commission.CommissionAmount);

        // Now simulate future admin changes:
        // 1. Tier rate drops from 8% -> 2%
        tier.Update("نقره‌ای", 10_000_000m, 2m, 10m);
        // 2. Affiliate gets a different VIP custom rate
        affiliate.SetCustomCommissionRate(1m);
        // 3. Category rate changes from 12% -> 4%
        categoryRatesAtOrder[drinksCategoryId] = 4m;

        // Approving the locked commission MUST preserve the exact order-time amount & snapshot!
        commission.Approve();
        Assert.Equal(36_800m, commission.CommissionAmount);

        var restored = AffiliateCommissionSnapshot.FromJson(commission.CalculationSnapshotJson);
        Assert.NotNull(restored);
        Assert.Equal("SG-9001", restored!.OrderTrackingCode);
        Assert.Equal(36_800m, restored.CommissionAmount);
        Assert.Equal(2, restored.Lines.Count);
        Assert.Equal(12m, restored.Lines[0].AppliedRatePercentage);
        Assert.Equal(120_000m, restored.Lines[0].UnitPriceAtOrder);
        Assert.Equal("نوشیدنی گرم", restored.Lines[0].CategoryName);
        Assert.Equal(5m, restored.Lines[1].AppliedRatePercentage);
        Assert.Equal(160_000m, restored.Lines[1].UnitPriceAtOrder);
    }

    [Fact]
    public void AccountingAccountCodes_ContainsAffiliateExpenseAndPayableSeedAccounts()
    {
        Assert.Contains(AccountingAccountCodes.SeedAccounts, a => a.Code == AccountingAccountCodes.AffiliatePayable && a.Type == AccountType.Liability);
        Assert.Contains(AccountingAccountCodes.SeedAccounts, a => a.Code == AccountingAccountCodes.AffiliateCommissionExpense && a.Type == AccountType.Expense);
    }
}
