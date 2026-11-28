using MediatR;
using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Sms;

public record SmsOtpReadinessDto(
    bool IsReadyForProduction,
    bool HasActiveProvider,
    bool IsLoggingOnly,
    Guid? ProviderId,
    string? ProviderName,
    string? ProviderType,
    string? LastConnectionError,
    DateTimeOffset? LastConnectionTestedAt,
    bool SupportsStatusRefresh,
    bool SupportsDeliveryWebhook,
    bool HasSenderLine,
    string? DefaultSenderLine,
    bool HasOtpTemplate,
    string? OtpTemplateName,
    bool UsesPatternSend,
    string? PatternTemplateId,
    string DeliveryModeLabel,
    IReadOnlyList<string> Warnings);

public record SmsOtpStatsDto(
    int TotalAttempts7d,
    int SentOrAccepted7d,
    int Delivered7d,
    int FailedOrRejected7d,
    int VerifiedByCustomer7d,
    int TotalOtpCodesGenerated7d);

public record SmsOtpLogItemDto(
    Guid Id,
    string Mobile,
    string DisplayMobile,
    string Status,
    bool VerifiedByCustomer,
    DateTimeOffset? VerifiedAtUtc,
    string? ProviderName,
    string? ProviderMessageId,
    string? ProviderResponse,
    string? ErrorMessage,
    DateTimeOffset RequestedAt,
    DateTimeOffset? SentAt,
    DateTimeOffset? LastStatusAt,
    bool CanRefreshStatus,
    string? RequestedBy);

public record SmsOtpDiagnosticsDto(
    SmsOtpReadinessDto Readiness,
    SmsOtpStatsDto Stats,
    IReadOnlyList<SmsOtpLogItemDto> RecentLogs);

public record GetSmsOtpDiagnosticsQuery(int Limit = 15) : IRequest<SmsOtpDiagnosticsDto>;

public class GetSmsOtpDiagnosticsQueryHandler(
    IApplicationDbContext dbContext,
    ISmsProviderResolver resolver,
    ISmsProviderGatewayFactory gatewayFactory) : IRequestHandler<GetSmsOtpDiagnosticsQuery, SmsOtpDiagnosticsDto>
{
    public async Task<SmsOtpDiagnosticsDto> Handle(
        GetSmsOtpDiagnosticsQuery request,
        CancellationToken cancellationToken)
    {
        var provider = await resolver.ResolveDefaultAsync(cancellationToken);
        var warnings = new List<string>();

        bool hasActiveProvider = provider is { IsEnabled: true };
        bool isLoggingOnly = provider is { ProviderType: SmsProviderType.Logging };
        bool supportsStatusRefresh = false;
        bool supportsDeliveryWebhook = false;
        bool supportsPatternSend = false;
        string? defaultLine = null;

        if (provider is not null)
        {
            defaultLine = provider.SenderLines.FirstOrDefault(l => l.IsDefaultLine)?.Number
                          ?? provider.SenderLines.FirstOrDefault()?.Number;
            var gateway = gatewayFactory.Create(resolver.Decrypt(provider));
            supportsStatusRefresh = gateway.SupportsMessageStatus;
            supportsDeliveryWebhook = gateway.SupportsDeliveryReport;
            supportsPatternSend = gateway.SupportsPatternSend;
        }

        var otpTemplate = await dbContext.SmsTemplates
            .AsNoTracking()
            .Include(t => t.ProviderMappings)
            .Where(t => t.IsActive && t.Kind == SmsMessageKind.Otp)
            .OrderByDescending(t => t.IsDefault)
            .ThenByDescending(t => t.LastModifiedAt ?? t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var mapping = provider is not null && otpTemplate is not null
            ? otpTemplate.ProviderMappings.FirstOrDefault(m =>
                m.ProviderType == provider.ProviderType && !string.IsNullOrWhiteSpace(m.ProviderTemplateId))
            : null;

        var usesPatternSend = supportsPatternSend && mapping is not null;

        if (!hasActiveProvider)
        {
            warnings.Add("هیچ سرویس‌دهنده پیامک فعالی به‌عنوان پیش‌فرض تنظیم نشده است؛ کد OTP برای مشتریان ارسال نخواهد شد.");
        }
        else if (isLoggingOnly)
        {
            warnings.Add("سرویس‌دهنده فعال روی حالت آزمایشی (Logging) است؛ پیامک واقعی به گوشی مشتری ارسال نمی‌شود.");
        }
        else
        {
            if (!usesPatternSend && string.IsNullOrWhiteSpace(defaultLine))
            {
                warnings.Add("خط ارسال پیش‌فرض و همچنین پترن OTP (Verify) تنظیم نشده است؛ ممکن است ارسال با خطا مواجه شود.");
            }
            else if (!usesPatternSend)
            {
                warnings.Add("پترن سریع (Verify/Lookup) برای این سرویس‌دهنده مپ نشده است؛ کد OTP با خط عادی ارسال می‌شود و ممکن است برای شماره‌های بلک‌لیست تبلیغاتی نرسد.");
            }
        }

        var deliveryModeLabel = !hasActiveProvider
            ? "غیرفعال"
            : isLoggingOnly
                ? "آزمایشی (فقط ثبت در لاگ)"
                : usesPatternSend
                    ? $"پترن سریع خدماتی (کد قالب: {mapping!.ProviderTemplateId})"
                    : $"ارسال متنی با خط {defaultLine ?? "پیش‌فرض"}";

        var readiness = new SmsOtpReadinessDto(
            IsReadyForProduction: hasActiveProvider && !isLoggingOnly && (usesPatternSend || !string.IsNullOrWhiteSpace(defaultLine)),
            HasActiveProvider: hasActiveProvider,
            IsLoggingOnly: isLoggingOnly,
            ProviderId: provider?.Id,
            ProviderName: provider?.Name,
            ProviderType: provider?.ProviderType.ToString(),
            LastConnectionError: provider?.LastConnectionError,
            LastConnectionTestedAt: provider?.LastSuccessfulConnectionAt,
            SupportsStatusRefresh: supportsStatusRefresh,
            SupportsDeliveryWebhook: supportsDeliveryWebhook,
            HasSenderLine: !string.IsNullOrWhiteSpace(defaultLine),
            DefaultSenderLine: defaultLine,
            HasOtpTemplate: otpTemplate is not null,
            OtpTemplateName: otpTemplate?.Name,
            UsesPatternSend: usesPatternSend,
            PatternTemplateId: mapping?.ProviderTemplateId,
            DeliveryModeLabel: deliveryModeLabel,
            Warnings: warnings);

        var since7d = DateTimeOffset.UtcNow.AddDays(-7);

        var otpMessages7d = await dbContext.SmsMessages
            .AsNoTracking()
            .Where(m => m.Kind == SmsMessageKind.Otp && m.RequestedAt >= since7d)
            .Select(m => m.Status)
            .ToListAsync(cancellationToken);

        var otpCodes7d = await dbContext.CustomerOtpCodes
            .AsNoTracking()
            .Where(c => c.CreatedAtUtc >= since7d)
            .Select(c => new { c.PhoneNumber, c.CreatedAtUtc, c.ConsumedAtUtc })
            .ToListAsync(cancellationToken);

        var stats = new SmsOtpStatsDto(
            TotalAttempts7d: otpMessages7d.Count,
            SentOrAccepted7d: otpMessages7d.Count(s => s is SmsSendStatus.Sent or SmsSendStatus.Accepted or SmsSendStatus.Delivered),
            Delivered7d: otpMessages7d.Count(s => s == SmsSendStatus.Delivered),
            FailedOrRejected7d: otpMessages7d.Count(s => s is SmsSendStatus.Failed or SmsSendStatus.Rejected),
            VerifiedByCustomer7d: otpCodes7d.Count(c => c.ConsumedAtUtc.HasValue),
            TotalOtpCodesGenerated7d: otpCodes7d.Count);

        var limit = Math.Clamp(request.Limit, 5, 50);
        var recentMessages = await dbContext.SmsMessages
            .AsNoTracking()
            .Where(m => m.Kind == SmsMessageKind.Otp)
            .OrderByDescending(m => m.RequestedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var providerMap = await dbContext.SmsProviders
            .AsNoTracking()
            .ToDictionaryAsync(p => p.Id, p => new { p.Name, p.ProviderType }, cancellationToken);

        var recentLogs = recentMessages.Select(m =>
        {
            var displayMobile = SmsTextUtilities.ToDisplayMobile(m.Mobile);
            // Match a consumed CustomerOtpCode for the same phone within +/- 5 minutes of this SMS
            var matchedOtp = otpCodes7d
                .Where(c => c.ConsumedAtUtc.HasValue
                            && (c.PhoneNumber == displayMobile || SmsTextUtilities.NormalizeMobile(c.PhoneNumber) == m.Mobile)
                            && Math.Abs((c.CreatedAtUtc - m.RequestedAt).TotalMinutes) <= 5)
                .OrderBy(c => Math.Abs((c.CreatedAtUtc - m.RequestedAt).TotalSeconds))
                .FirstOrDefault();

            var provInfo = m.ProviderId is { } pid && providerMap.TryGetValue(pid, out var info) ? info : null;
            var canRefresh = !string.IsNullOrWhiteSpace(m.ProviderMessageId)
                             && provInfo is not null
                             && provInfo.ProviderType != SmsProviderType.Logging;

            return new SmsOtpLogItemDto(
                m.Id,
                m.Mobile,
                displayMobile,
                m.Status.ToString(),
                VerifiedByCustomer: matchedOtp is not null,
                VerifiedAtUtc: matchedOtp?.ConsumedAtUtc,
                ProviderName: provInfo?.Name,
                ProviderMessageId: m.ProviderMessageId,
                ProviderResponse: m.ProviderResponse,
                ErrorMessage: m.ErrorMessage,
                RequestedAt: m.RequestedAt,
                SentAt: m.SentAt,
                LastStatusAt: m.LastStatusAt,
                CanRefreshStatus: canRefresh,
                RequestedBy: m.RequestedBy);
        }).ToList();

        return new SmsOtpDiagnosticsDto(readiness, stats, recentLogs);
    }
}

public record SendTestOtpSmsCommand(string Mobile) : IRequest<SendTestOtpSmsResultDto>;

public record SendTestOtpSmsResultDto(
    bool Success,
    Guid? MessageId,
    string Status,
    string DisplayMobile,
    string TestCode,
    string? ProviderMessageId,
    string? Error,
    string Summary);

public class SendTestOtpSmsCommandHandler(
    IApplicationDbContext dbContext,
    ISmsGateway smsGateway) : IRequestHandler<SendTestOtpSmsCommand, SendTestOtpSmsResultDto>
{
    public async Task<SendTestOtpSmsResultDto> Handle(
        SendTestOtpSmsCommand request,
        CancellationToken cancellationToken)
    {
        var normalized = SmsTextUtilities.NormalizeMobile(request.Mobile);
        if (normalized is null)
        {
            return new SendTestOtpSmsResultDto(
                Success: false,
                MessageId: null,
                Status: SmsSendStatus.Failed.ToString(),
                DisplayMobile: request.Mobile ?? string.Empty,
                TestCode: string.Empty,
                ProviderMessageId: null,
                Error: "شماره موبایل معتبر وارد کنید (مثلاً 09123456789).",
                Summary: "شماره موبایل نامعتبر است.");
        }

        var testCode = CustomerOtpCode.GenerateNumericCode(CustomerOtpCode.DefaultCodeLength);
        var cafeName = await dbContext.SiteSettings
            .AsNoTracking()
            .Select(s => s.CafeName)
            .FirstOrDefaultAsync(cancellationToken);

        var outcome = await smsGateway.SendOtpAsync(
            normalized,
            testCode,
            cafeName,
            recipientName: "کاربر آزمایشی",
            cancellationToken: cancellationToken);

        var summary = outcome.Success
            ? $"درخواست ارسال کد آزمایشی {testCode} توسط سرویس‌دهنده پذیرفته شد. برای اطمینان از رسیدن به گوشی، چند ثانیه بعد روی «استعلام تحویل» کلیک کنید."
            : $"ارسال کد آزمایشی ناموفق بود: {outcome.Error ?? "خطای نامشخص"}";

        return new SendTestOtpSmsResultDto(
            Success: outcome.Success,
            MessageId: outcome.MessageId,
            Status: outcome.Status.ToString(),
            DisplayMobile: outcome.NormalizedMobile,
            TestCode: testCode,
            ProviderMessageId: outcome.ProviderMessageId,
            Error: outcome.Error,
            Summary: summary);
    }
}
