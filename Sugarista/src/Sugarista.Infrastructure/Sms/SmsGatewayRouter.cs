using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Common.Options;
using Sugarista.Application.Sms;
using Microsoft.Extensions.Options;
using Sugarista.Infrastructure.Notifications;

namespace Sugarista.Infrastructure.Sms;

/// <summary>
/// Routes the two legacy notification channels (order-ready / pickup-change
/// and maintenance reminders) through the SMS Management Center whenever a
/// DB-configured provider exists, and falls back to the original
/// appsettings-driven services otherwise. Zero behaviour change for
/// deployments that never touch the new module.
/// </summary>
public sealed class SmsGatewayRouter(
    IApplicationDbContext dbContext,
    ISmsGateway smsGateway,
    IOptions<SmsOptions> smsOptions,
    KavenegarSmsService legacyKavenegarService,
    LoggingSmsService legacyLoggingService,
    KavenegarMaintenanceSmsService legacyKavenegarMaintenanceService,
    LoggingMaintenanceSmsService legacyLoggingMaintenanceService) : ISmsService, IMaintenanceSmsSender
{
    public async Task SendOrderReadyAsync(
        string phoneNumber,
        string trackingCode,
        string customerName,
        CancellationToken cancellationToken = default)
    {
        if (await UseGatewayAsync(cancellationToken))
        {
            var body = $"سلام {customerName} عزیز، سفارش شما با کد {trackingCode} آماده شد. نوشیدنی‌های شوگاریستا در انتظار شماست.";
            await smsGateway.SendSystemAsync(phoneNumber, body, "order-ready", cancellationToken);
            return;
        }

        if (UseLegacyKavenegar())
        {
            await legacyKavenegarService.SendOrderReadyAsync(phoneNumber, trackingCode, customerName, cancellationToken);
            return;
        }

        await legacyLoggingService.SendOrderReadyAsync(phoneNumber, trackingCode, customerName, cancellationToken);
    }

    public async Task SendPickupScheduleChangedAsync(
        string phoneNumber,
        string trackingCode,
        string customerName,
        string deliveryTimeLabel,
        CancellationToken cancellationToken = default)
    {
        if (await UseGatewayAsync(cancellationToken))
        {
            var body = $"سلام {customerName} عزیز، زمان دریافت سفارش {trackingCode} به {deliveryTimeLabel} تغییر کرد.";
            await smsGateway.SendSystemAsync(phoneNumber, body, "pickup-changed", cancellationToken);
            return;
        }

        if (UseLegacyKavenegar())
        {
            await legacyKavenegarService.SendPickupScheduleChangedAsync(
                phoneNumber, trackingCode, customerName, deliveryTimeLabel, cancellationToken);
            return;
        }

        await legacyLoggingService.SendPickupScheduleChangedAsync(
            phoneNumber, trackingCode, customerName, deliveryTimeLabel, cancellationToken);
    }

    public async Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
    {
        if (await UseGatewayAsync(cancellationToken))
        {
            await smsGateway.SendSystemAsync(phoneNumber, message, "maintenance-reminder", cancellationToken);
            return;
        }

        if (UseLegacyKavenegar())
        {
            await legacyKavenegarMaintenanceService.SendAsync(phoneNumber, message, cancellationToken);
            return;
        }

        await legacyLoggingMaintenanceService.SendAsync(phoneNumber, message, cancellationToken);
    }

    private async Task<bool> UseGatewayAsync(CancellationToken cancellationToken) =>
        await dbContext.SmsProviders.AsNoTracking().AnyAsync(provider => provider.IsEnabled, cancellationToken);

    private bool UseLegacyKavenegar() =>
        string.Equals(smsOptions.Value.Provider, "Kavenegar", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(smsOptions.Value.ApiKey);
}
