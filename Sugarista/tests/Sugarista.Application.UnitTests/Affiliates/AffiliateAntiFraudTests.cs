using Sugarista.Application.Affiliates.Services;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.UnitTests.Affiliates;

public class AffiliateAntiFraudTests
{
    [Theory]
    [InlineData("۰۹۱۲۳۴۵۶۷۸۹", "09123456789")]
    [InlineData("+98 912-345-6789", "09123456789")]
    [InlineData("00989123456789", "09123456789")]
    [InlineData("989123456789", "09123456789")]
    [InlineData("9123456789", "09123456789")]
    [InlineData("09123456789", "09123456789")]
    public void NormalizePhone_CanonicalizesAllIranianFormats(string raw, string expected)
    {
        Assert.Equal(expected, AffiliateAntiFraudEvaluator.NormalizePhone(raw));
    }

    [Fact]
    public void DetectSelfReferral_CatchesMatchingPhoneAcrossFormats()
    {
        var buyer = new AffiliateIdentitySnapshot(Guid.NewGuid(), "+98 912 111 2233");
        var owner = new AffiliateIdentitySnapshot(Guid.NewGuid(), "۰۹۱۲۱۱۱۲۲۳۳");

        var result = AffiliateAntiFraudEvaluator.DetectSelfReferral(buyer, owner);

        Assert.True(result.IsSelfReferral);
        Assert.Equal("PhoneNumber", result.MatchedSignal);
    }

    [Fact]
    public void DetectSelfReferral_CatchesNationalIdAndDeliveryAddressAndCustomerId()
    {
        var sameId = Guid.NewGuid();
        Assert.Equal("CustomerId", AffiliateAntiFraudEvaluator.DetectSelfReferral(
            new AffiliateIdentitySnapshot(sameId, "09120000001"),
            new AffiliateIdentitySnapshot(sameId, "09120000002")).MatchedSignal);

        Assert.Equal("NationalId", AffiliateAntiFraudEvaluator.DetectSelfReferral(
            new AffiliateIdentitySnapshot(Guid.NewGuid(), "09120000001", NationalId: "۰۰۱۲۳۴۵۶۷۸"),
            new AffiliateIdentitySnapshot(Guid.NewGuid(), "09120000002", NationalId: "0012345678")).MatchedSignal);

        Assert.Equal("DeliveryAddress", AffiliateAntiFraudEvaluator.DetectSelfReferral(
            new AffiliateIdentitySnapshot(Guid.NewGuid(), "09120000001", DeliveryAddress: "تهران، خیابان ولیعصر، پلاک ۱۲"),
            new AffiliateIdentitySnapshot(Guid.NewGuid(), "09120000002", DeliveryAddress: "تهران،   خیابان ولیعصر، پلاک 12")).MatchedSignal);
    }

    [Fact]
    public void DetectSelfReferral_AllowsDistinctBuyerAndAffiliate()
    {
        var buyer = new AffiliateIdentitySnapshot(
            Guid.NewGuid(),
            "09121111111",
            Email: "buyer@example.com",
            NationalId: "0011111111",
            DeliveryAddress: "تهران، خیابان شریعتی، پلاک ۱۰");

        var owner = new AffiliateIdentitySnapshot(
            Guid.NewGuid(),
            "09122222222",
            Email: "affiliate@example.com",
            NationalId: "0022222222",
            DeliveryAddress: "تهران، خیابان انقلاب، پلاک ۵۰");

        var result = AffiliateAntiFraudEvaluator.DetectSelfReferral(buyer, owner);
        Assert.False(result.IsSelfReferral);
        Assert.Null(result.MatchedSignal);
    }

    [Fact]
    public void IsDuplicateClickWithinWindow_BlocksWithin1HourAndAllowsAfter1Hour()
    {
        var firstClick = new DateTimeOffset(2026, 11, 27, 10, 0, 0, TimeSpan.Zero);

        Assert.True(AffiliateAntiFraudEvaluator.IsDuplicateClickWithinWindow(firstClick, firstClick.AddMinutes(1)));
        Assert.True(AffiliateAntiFraudEvaluator.IsDuplicateClickWithinWindow(firstClick, firstClick.AddMinutes(59).AddSeconds(59)));
        Assert.False(AffiliateAntiFraudEvaluator.IsDuplicateClickWithinWindow(firstClick, firstClick.AddHours(1)));
        Assert.False(AffiliateAntiFraudEvaluator.IsDuplicateClickWithinWindow(null, firstClick));
    }

    [Fact]
    public void BuildPhoneLookupVariants_IncludesAllCanonicalVariantsForFirstOrderCheck()
    {
        var variants = AffiliateAntiFraudEvaluator.BuildPhoneLookupVariants("۰۹۱۲۳۴۵۶۷۸۹");

        Assert.Contains("09123456789", variants);
        Assert.Contains("+989123456789", variants);
        Assert.Contains("989123456789", variants);
        Assert.Contains("00989123456789", variants);
    }

    [Fact]
    public void CommissionLifecycle_PendingApproveAndCancelReversal_MaintainsExactBalance()
    {
        var affiliate = new Affiliate(Guid.NewGuid(), status: AffiliateStatus.Active);
        var commission = new AffiliateCommission(
            affiliate.Id,
            Guid.NewGuid(),
            AffiliateOrderSource.Online,
            500_000m,
            450_000m,
            45_000m,
            AffiliateCommissionStatus.Pending);

        // Delivered / Completed -> Approved + Credit CashBalance
        commission.Approve();
        affiliate.CreditCashBalance(commission.CommissionAmount);
        Assert.Equal(AffiliateCommissionStatus.Approved, commission.Status);
        Assert.Equal(45_000m, affiliate.CashBalance);

        // Later Cancelled / Refunded -> Rejected + Reverse CashBalance
        affiliate.DebitCashBalance(commission.CommissionAmount);
        commission.Reject("سفارش لغو یا مرجوع شد.");
        Assert.Equal(AffiliateCommissionStatus.Rejected, commission.Status);
        Assert.Equal(0m, affiliate.CashBalance);
    }
}
