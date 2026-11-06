using Sugarista.Application.Discounts.Reports;
using Sugarista.Application.Discounts.Security;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.UnitTests;

public class DiscountSecurityAndReportingTests
{
    private static Order CreateOrder(decimal unitPrice = 200_000m, int quantity = 1)
    {
        var order = new Order(
            "SG-PHASE6",
            "مهمان آزمایشی",
            "09120000000",
            null,
            totalAmount: 1m,
            paymentAuthority: "authority",
            SplitBillMode.Single);

        order.AddLine(new OrderLine(
            order.Id,
            Guid.NewGuid(),
            "لاته",
            "Latte",
            null,
            null,
            quantity,
            unitPrice,
            []));

        order.RecalculateTotal();
        return order;
    }

    [Fact]
    public void SplitByCategory_ShouldPreserveExactAmountAfterRounding()
    {
        var rows = OrderDiscountTracker.SplitByCategory(
            100m,
            [
                new DiscountScopeAmount(Guid.NewGuid(), 1m),
                new DiscountScopeAmount(Guid.NewGuid(), 1m),
                new DiscountScopeAmount(Guid.NewGuid(), 1m),
            ]);

        Assert.Equal(3, rows.Count);
        Assert.Equal(100m, rows.Sum(row => row.Amount));
    }

    [Fact]
    public void Tracker_ShouldStackBenefitsAndClampAtDailyCap()
    {
        var order = CreateOrder(unitPrice: 300_000m);
        var tracker = new OrderDiscountTracker(order, Guid.NewGuid(), remainingCap: 100_000m);

        var first = tracker.Apply(OrderDiscountKind.Group, "گروه ویژه", 60_000m);
        var second = tracker.Apply(OrderDiscountKind.Cup, "لیوان شخصی", 60_000m);
        var third = tracker.Apply(OrderDiscountKind.Birthday, "هدیهٔ تولد", 10_000m);

        Assert.Equal(60_000m, first);
        Assert.Equal(40_000m, second);
        Assert.Equal(0m, third);
        Assert.Equal(100_000m, tracker.AppliedTotal);
        Assert.Equal(0m, tracker.RemainingCap);
        Assert.Equal(200_000m, order.TotalAmount);
        Assert.Equal(100_000m, order.DiscountTotal);
        Assert.Equal(2, order.Discounts.Count);
        Assert.All(order.Discounts, discount => Assert.True(discount.Amount > 0));
    }

    [Fact]
    public void Tracker_ShouldNeverApplyMoreThanOrderTotal()
    {
        var order = CreateOrder(unitPrice: 50_000m);
        var tracker = new OrderDiscountTracker(order, Guid.NewGuid(), remainingCap: null);

        var applied = tracker.Apply(OrderDiscountKind.Code, "کد بزرگ", 500_000m);

        Assert.Equal(50_000m, applied);
        Assert.Equal(0m, order.TotalAmount);
        Assert.Equal(50_000m, order.DiscountTotal);
    }

    [Fact]
    public void DailyCapRange_ShouldCoverTehranLocalDay()
    {
        var now = new DateTimeOffset(2026, 11, 4, 20, 0, 0, TimeSpan.Zero); // 23:30 Tehran
        var (start, end) = DiscountDailyCapService.GetLocalDayRange(now, "Asia/Tehran");

        Assert.Equal(new DateTimeOffset(2026, 11, 3, 20, 30, 0, TimeSpan.Zero), start);
        Assert.Equal(new DateTimeOffset(2026, 11, 4, 20, 30, 0, TimeSpan.Zero), end);
    }

    [Fact]
    public void DiscountExpiry_ShouldBeEvaluatedAtTheMomentOfPayment()
    {
        var expiresAt = new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
        var discount = new Discount(
            "کد زمان‌دار",
            null,
            "NOW10",
            DiscountType.AmountOff,
            DiscountValueKind.Percentage,
            10m,
            null,
            0m,
            null,
            expiresAt,
            true,
            0,
            true,
            null,
            null,
            DiscountAppliesTo.Order,
            DiscountCustomerGroup.All);

        Assert.True(discount.IsLive(expiresAt.AddSeconds(-1)));
        Assert.False(discount.IsLive(expiresAt.AddSeconds(1)));
    }

    [Fact]
    public void OrderDiscount_ShouldRejectNegativeAmount()
    {
        Assert.Throws<DomainException>(() => new OrderDiscount(
            Guid.NewGuid(),
            Guid.NewGuid(),
            OrderDiscountKind.Code,
            "کد",
            -1m));
    }

    [Fact]
    public void ReportSchedule_ShouldFireOncePerPeriod()
    {
        var schedule = new ReportSchedule(
            "گزارش هفتگی",
            "discount-report",
            ReportFrequency.Weekly,
            hour: 9,
            recipientEmails: "owner@example.com",
            dayOfWeek: 6);

        var due = new DateTimeOffset(2026, 11, 7, 9, 5, 0, TimeSpan.FromHours(3.5)); // Saturday
        Assert.True(schedule.IsDue(due));

        schedule.MarkRun(due);
        Assert.False(schedule.IsDue(due));

        var nextWeek = due.AddDays(7);
        Assert.True(schedule.IsDue(nextWeek));
    }

    [Fact]
    public void ReportSchedule_ShouldParseRecipientsAndRejectEmptyEmail()
    {
        var schedule = new ReportSchedule(
            "گزارش روزانه",
            "discount-report",
            ReportFrequency.Daily,
            hour: 8,
            recipientEmails: "a@example.com, b@example.com; c@example.com");

        Assert.Equal(3, schedule.Recipients().Count);

        Assert.Throws<DomainException>(() => new ReportSchedule(
            "بدون ایمیل",
            "discount-report",
            ReportFrequency.Daily,
            hour: 8,
            recipientEmails: "  "));
    }

    [Fact]
    public void ReportColumns_ShouldFallBackToAllWhenSelectionIsEmpty()
    {
        var columns = DiscountReportColumns.Normalize([]);
        Assert.Equal(DiscountReportColumns.All.Count, columns.Count);
    }
}
