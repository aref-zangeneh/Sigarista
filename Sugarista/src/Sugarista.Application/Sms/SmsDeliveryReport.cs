using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Sms;

/// <summary>
/// Parsed delivery-report webhook payload. Providers differ wildly in format
/// (form-encoded, JSON, query params) so the controller normalizes everything
/// into these raw fields and this parser maps them to the standard lifecycle.
/// </summary>
public static class SmsDeliveryStatusParser
{
    /// <summary>Maps a raw status to the standard outcome. Text beats numbers: providers send both.</summary>
    public static SmsOutcomeStatus Parse(
        SmsProviderType providerType,
        string? statusText,
        int? statusCode)
    {
        if (!string.IsNullOrWhiteSpace(statusText))
        {
            switch (statusText.Trim().ToLowerInvariant())
            {
                case "delivered":
                    return SmsOutcomeStatus.Delivered;
                case "sent":
                case "queued":
                    return SmsOutcomeStatus.Sent;
                case "failed":
                case "error":
                    return SmsOutcomeStatus.Failed;
                case "rejected":
                case "blacklist":
                case "blocked":
                    return SmsOutcomeStatus.Rejected;
            }
        }

        if (statusCode is { } code)
        {
            return providerType switch
            {
                // Kavenegar delivery status codes.
                SmsProviderType.Kavenegar => code switch
                {
                    1 or 2 or 4 or 10 => SmsOutcomeStatus.Sent,
                    5 or 8 => SmsOutcomeStatus.Delivered,
                    6 or 13 => SmsOutcomeStatus.Failed,
                    11 or 14 => SmsOutcomeStatus.Rejected,
                    _ => SmsOutcomeStatus.Unknown
                },
                // SMS.ir / Ghasedak / IPPanel-style 1=delivered convention.
                _ => code switch
                {
                    1 => SmsOutcomeStatus.Delivered,
                    2 => SmsOutcomeStatus.Failed,
                    _ => SmsOutcomeStatus.Unknown
                }
            };
        }

        return SmsOutcomeStatus.Unknown;
    }
}

public sealed record SmsDeliveryReportData(
    Guid ProviderId,
    string Secret,
    string? MessageId,
    string? StatusText,
    int? StatusCode,
    string? Receptor);

public record RecordSmsDeliveryReportCommand(SmsDeliveryReportData Data) : IRequest<SmsDeliveryReportAck>;

public sealed record SmsDeliveryReportAck(bool Handled, string Message);

public class RecordSmsDeliveryReportCommandValidator : AbstractValidator<RecordSmsDeliveryReportCommand>
{
    public RecordSmsDeliveryReportCommandValidator()
    {
        RuleFor(command => command.Data.ProviderId).NotEqual(Guid.Empty);
        RuleFor(command => command.Data.Secret).NotEmpty().MaximumLength(128);
        RuleFor(command => command.Data)
            .Must(data => !string.IsNullOrWhiteSpace(data.MessageId) || !string.IsNullOrWhiteSpace(data.Receptor))
            .WithMessage("شناسهٔ پیام یا شمارهٔ گیرنده باید در گزارش ارسال شود.");
    }
}

/// <summary>
/// Applies an inbound provider delivery report: only DELIVERED/FAILED/REJECTED
/// transitions land here; an already-Delivered message is never downgraded and
/// duplicate reports are acknowledged idempotently.
/// </summary>
public class RecordSmsDeliveryReportCommandHandler(
    IApplicationDbContext dbContext,
    ISmsProviderResolver resolver,
    IAuditLog auditLog)
    : IRequestHandler<RecordSmsDeliveryReportCommand, SmsDeliveryReportAck>
{
    public async Task<SmsDeliveryReportAck> Handle(
        RecordSmsDeliveryReportCommand request,
        CancellationToken cancellationToken)
    {
        var provider = await dbContext.SmsProviders
            .FirstOrDefaultAsync(item => item.Id == request.Data.ProviderId, cancellationToken);
        if (provider is null || string.IsNullOrWhiteSpace(provider.DeliveryWebhookSecret)
            || !ConstantTimeEquals(provider.DeliveryWebhookSecret, request.Data.Secret))
        {
            // Generic ack: never leak which part failed to unknown callers.
            return new SmsDeliveryReportAck(false, "درخواست پذیرفته نشد.");
        }

        var outcome = SmsDeliveryStatusParser.Parse(provider.ProviderType, request.Data.StatusText, request.Data.StatusCode);
        var targetStatus = outcome switch
        {
            SmsOutcomeStatus.Delivered => SmsSendStatus.Delivered,
            SmsOutcomeStatus.Failed => SmsSendStatus.Failed,
            SmsOutcomeStatus.Rejected => SmsSendStatus.Rejected,
            SmsOutcomeStatus.Sent => SmsSendStatus.Sent,
            _ => (SmsSendStatus?)null
        };

        if (targetStatus is null)
        {
            return new SmsDeliveryReportAck(false, "وضعیت گزارش قابل تفسیر نبود و نادیده گرفته شد.");
        }

        // Match by provider message id first, fall back to provider+receptor.
        var message = await FindMessageAsync(request.Data, provider.Id, cancellationToken);
        if (message is null)
        {
            await resolver.RecordLogAsync(
                provider, "delivery-report", false,
                $"Unmatched report: id={request.Data.MessageId ?? "-"}, receptor={request.Data.Receptor ?? "-"}, status={outcome}.",
                cancellationToken);
            return new SmsDeliveryReportAck(false, "پیامک مطابق با این گزارش پیدا نشد.");
        }

        // Terminal protection: Delivered is never downgraded, and a final
        // Failed/Rejected outcome is never overwritten by a later report.
        if (message.Status is SmsSendStatus.Delivered or SmsSendStatus.Failed or SmsSendStatus.Rejected)
        {
            return new SmsDeliveryReportAck(true, "وضعیت پیامک از قبل نهایی است.");
        }

        message.MarkDelivery(targetStatus.Value);
        await dbContext.SaveChangesAsync(cancellationToken);
        await resolver.RecordLogAsync(
            provider, "delivery-report", true,
            $"{message.Mobile} → {targetStatus} (id={request.Data.MessageId ?? "-"}).",
            cancellationToken);
        auditLog.Write("sms.delivery-report", $"Provider '{provider.Name}', message {message.Id} → {targetStatus}.");
        return new SmsDeliveryReportAck(true, "وضعیت پیامک به‌روزرسانی شد.");
    }

    private async Task<SmsMessage?> FindMessageAsync(
        SmsDeliveryReportData data,
        Guid providerId,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(data.MessageId))
        {
            var byId = await dbContext.SmsMessages
                .FirstOrDefaultAsync(message => message.ProviderMessageId == data.MessageId, cancellationToken);
            if (byId is not null)
            {
                return byId;
            }
        }

        var normalized = SmsTextUtilities.NormalizeMobile(data.Receptor);
        if (normalized is not null)
        {
            var byReceptor = await dbContext.SmsMessages
                .Where(message => message.ProviderId == providerId && message.Mobile == normalized)
                .OrderByDescending(message => message.RequestedAt)
                .FirstOrDefaultAsync(cancellationToken);
            return byReceptor;
        }

        return null;
    }

    private static bool ConstantTimeEquals(string left, string right)
    {
        // Length-independent comparison to avoid trivial timing probes.
        var a = System.Text.Encoding.UTF8.GetBytes(left);
        var b = System.Text.Encoding.UTF8.GetBytes(right);
        var max = Math.Max(a.Length, b.Length);
        var diff = a.Length ^ b.Length;
        for (var index = 0; index < max; index++)
        {
            diff |= (index < a.Length ? a[index] : 0) ^ (index < b.Length ? b[index] : 0);
        }
        return diff == 0;
    }
}
