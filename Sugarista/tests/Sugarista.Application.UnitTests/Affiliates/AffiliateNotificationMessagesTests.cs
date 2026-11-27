using Sugarista.Application.Affiliates.Services;

namespace Sugarista.Application.UnitTests.Affiliates;

public class AffiliateNotificationMessagesTests
{
    [Fact]
    public void AccountApproved_IncludesAffiliateNameAndOptionalCouponCode()
    {
        var withCoupon = AffiliateNotificationMessages.AccountApproved("علی", "ALI15");
        Assert.Contains("علی", withCoupon);
        Assert.Contains("ALI15", withCoupon);

        var withoutCoupon = AffiliateNotificationMessages.AccountApproved("سارا");
        Assert.Contains("سارا", withoutCoupon);
        Assert.Contains("تایید", withoutCoupon);
    }

    [Fact]
    public void NewOrderPendingCommission_IncludesTrackingCodeAndPendingAmount()
    {
        var msg = AffiliateNotificationMessages.NewOrderPendingCommission("علی", "SG-1042", 45_000m);
        Assert.Contains("SG-1042", msg);
        Assert.Contains("45,000", msg);
        Assert.Contains("معلق", msg);
    }

    [Fact]
    public void CommissionApproved_IncludesApprovedAmountAndNewBalance()
    {
        var msg = AffiliateNotificationMessages.CommissionApproved("علی", "SG-1042", 45_000m, 245_000m);
        Assert.Contains("SG-1042", msg);
        Assert.Contains("45,000", msg);
        Assert.Contains("245,000", msg);
    }

    [Fact]
    public void PayoutCompleted_IncludesAmountAndBankTrackingCode()
    {
        var msg = AffiliateNotificationMessages.PayoutCompleted("علی", 600_000m, "TRK-778899");
        Assert.Contains("600,000", msg);
        Assert.Contains("TRK-778899", msg);
    }
}
