using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sugarista.Application.Affiliates.Services;
using Sugarista.Application.Sms;

namespace Sugarista.Infrastructure.Notifications;

/// <summary>
/// Sends instant affiliate motivation & transparency notifications via
/// Sugarista's configured SMS gateway (e.g. SMS.ir) and optionally Telegram Bot API.
/// Best-effort: never throws into financial or order workflows.
/// </summary>
public sealed class AffiliateNotificationService : IAffiliateNotificationService
{
    private readonly ISmsGateway _smsGateway;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AffiliateNotificationService> _logger;

    public AffiliateNotificationService(
        ISmsGateway smsGateway,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<AffiliateNotificationService> logger)
    {
        _smsGateway = smsGateway;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public Task NotifyAccountApprovedAsync(
        string? phoneNumber,
        string affiliateName,
        string? couponCode = null,
        CancellationToken cancellationToken = default)
        => DispatchAsync(
            phoneNumber,
            AffiliateNotificationMessages.AccountApproved(affiliateName, couponCode),
            "affiliate.account_approved",
            cancellationToken);

    public Task NotifyNewOrderPendingCommissionAsync(
        string? phoneNumber,
        string affiliateName,
        string orderTrackingCode,
        decimal pendingCommissionAmount,
        CancellationToken cancellationToken = default)
        => DispatchAsync(
            phoneNumber,
            AffiliateNotificationMessages.NewOrderPendingCommission(affiliateName, orderTrackingCode, pendingCommissionAmount),
            "affiliate.order_pending",
            cancellationToken);

    public Task NotifyCommissionApprovedAsync(
        string? phoneNumber,
        string affiliateName,
        string orderTrackingCode,
        decimal approvedCommissionAmount,
        decimal newCashBalance,
        CancellationToken cancellationToken = default)
        => DispatchAsync(
            phoneNumber,
            AffiliateNotificationMessages.CommissionApproved(affiliateName, orderTrackingCode, approvedCommissionAmount, newCashBalance),
            "affiliate.commission_approved",
            cancellationToken);

    public Task NotifyPayoutCompletedAsync(
        string? phoneNumber,
        string affiliateName,
        decimal amount,
        string? trackingCode,
        CancellationToken cancellationToken = default)
        => DispatchAsync(
            phoneNumber,
            AffiliateNotificationMessages.PayoutCompleted(affiliateName, amount, trackingCode),
            "affiliate.payout_completed",
            cancellationToken);

    private async Task DispatchAsync(
        string? phoneNumber,
        string message,
        string action,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(phoneNumber))
        {
            try
            {
                await _smsGateway.SendSystemAsync(phoneNumber, message, action, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Affiliate SMS notification failed for action {Action}", action);
            }
        }

        var botToken = _configuration["AffiliateNotifications:TelegramBotToken"];
        var chatId = _configuration["AffiliateNotifications:TelegramChatId"];
        if (!string.IsNullOrWhiteSpace(botToken) && !string.IsNullOrWhiteSpace(chatId))
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var url = $"https://api.telegram.org/bot{botToken.Trim()}/sendMessage";
                await client.PostAsJsonAsync(
                    url,
                    new { chat_id = chatId.Trim(), text = message },
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Affiliate Telegram notification failed for action {Action}", action);
            }
        }
    }
}
