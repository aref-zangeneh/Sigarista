namespace Sugarista.Application.Affiliates.Services;

public static class AffiliateNotificationMessages
{
    public static string AccountApproved(string affiliateName, string? couponCode = null)
    {
        var baseMsg = $"تبریک {affiliateName} عزیز! حساب همکاری در فروش شما در کافه شوگاریستا تایید و فعال شد.";
        return string.IsNullOrWhiteSpace(couponCode)
            ? $"{baseMsg} اکنون می‌توانید از پنل اختصاصی خود لینک و QR بسازید."
            : $"{baseMsg} کد تخفیف اختصاصی شما: {couponCode}";
    }

    public static string NewOrderPendingCommission(
        string affiliateName,
        string orderTrackingCode,
        decimal pendingCommissionAmount)
        => $"{affiliateName} عزیز، یک سفارش جدید ({orderTrackingCode}) از طریق لینک/کد شما ثبت شد. پورسانت معلق: {pendingCommissionAmount:N0} تومان (پس از تحویل سفارش به موجودی شما اضافه می‌شود).";

    public static string CommissionApproved(
        string affiliateName,
        string orderTrackingCode,
        decimal approvedCommissionAmount,
        decimal newCashBalance)
        => $"{affiliateName} عزیز، سفارش {orderTrackingCode} نهایی شد و مبلغ {approvedCommissionAmount:N0} تومان پورسانت به موجودی شما واریز گردید. موجودی فعلی: {newCashBalance:N0} تومان.";

    public static string PayoutCompleted(
        string affiliateName,
        decimal amount,
        string? trackingCode)
    {
        var refPart = string.IsNullOrWhiteSpace(trackingCode)
            ? string.Empty
            : $" کد رهگیری بانکی: {trackingCode}";
        return $"{affiliateName} عزیز، مبلغ {amount:N0} تومان تسویه حساب همکاری در فروش به شماره شبای شما واریز شد.{refPart}";
    }
}

public interface IAffiliateNotificationService
{
    Task NotifyAccountApprovedAsync(
        string? phoneNumber,
        string affiliateName,
        string? couponCode = null,
        CancellationToken cancellationToken = default);

    Task NotifyNewOrderPendingCommissionAsync(
        string? phoneNumber,
        string affiliateName,
        string orderTrackingCode,
        decimal pendingCommissionAmount,
        CancellationToken cancellationToken = default);

    Task NotifyCommissionApprovedAsync(
        string? phoneNumber,
        string affiliateName,
        string orderTrackingCode,
        decimal approvedCommissionAmount,
        decimal newCashBalance,
        CancellationToken cancellationToken = default);

    Task NotifyPayoutCompletedAsync(
        string? phoneNumber,
        string affiliateName,
        decimal amount,
        string? trackingCode,
        CancellationToken cancellationToken = default);
}
