using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Sms;
using Sugarista.Domain.Entities;
using Sugarista.Infrastructure.Settings;

namespace Sugarista.Infrastructure.Sms;

/// <summary>
/// The send orchestrator behind every SMS path (single, bulk/system, preview,
/// campaign creation). Rules baked in:
///   • mobile normalization + deduplication + opt-out / inactive filtering,
///   • marketing consent enforcement,
///   • daily-limit / max-recipients guards,
///   • NO failover or retry on ambiguous timeouts (duplicate-send protection),
///   • NO failover on provider REJECTED (validation-type) results,
///   • every attempt recorded per recipient in SmsMessage + SmsProviderLog.
/// </summary>
public sealed class SmsGateway(
    IApplicationDbContext dbContext,
    ISmsProviderResolver resolver,
    ISmsProviderGatewayFactory gatewayFactory,
    AppSetupStore appSetupStore,
    ILogger<SmsGateway> logger) : ISmsGateway
{
    public async Task<SmsSendOutcome> SendSingleAsync(
        SmsSendRequest request,
        CancellationToken cancellationToken = default)
    {
        var mobile = SmsTextUtilities.NormalizeMobile(request.Mobile);
        if (mobile is null)
        {
            return new SmsSendOutcome(false, null, SmsSendStatus.Failed, null, "شماره موبایل نامعتبر است.", string.Empty);
        }

        var body = await ResolveBodyAsync(request, mobile, cancellationToken);
        if (string.IsNullOrWhiteSpace(body))
        {
            return new SmsSendOutcome(false, null, SmsSendStatus.Failed, null,
                "متن پیامک خالی است؛ قالب را انتخاب کنید یا متن بنویسید.", SmsTextUtilities.ToDisplayMobile(mobile));
        }

        if (await DailyLimitReachedAsync(1, cancellationToken))
        {
            return new SmsSendOutcome(false, null, SmsSendStatus.Failed, null,
                "محدودیت ارسال روزانه پر شده است؛ فردا دوباره تلاش کنید یا سقف را در تنظیمات پیامک افزایش دهید.",
                SmsTextUtilities.ToDisplayMobile(mobile));
        }

        var provider = request.ProviderId is { } providerId
            ? await resolver.ResolveByIdAsync(providerId, cancellationToken)
            : await resolver.ResolveDefaultAsync(cancellationToken);
        if (provider is not { IsEnabled: true })
        {
            return new SmsSendOutcome(false, null, SmsSendStatus.Failed, null,
                "هیچ سرویس‌دهنده پیامک فعالی برای این ارسال پیدا نشد.", SmsTextUtilities.ToDisplayMobile(mobile));
        }

        var message = new SmsMessage(
            provider.Id, mobile, body, request.Kind, SmsSendType.Single, request.TemplateId,
            campaignId: null, batchNumber: 0, request.RequestedBy);
        dbContext.SmsMessages.Add(message);

        var settings = appSetupStore.GetSmsSettings();
        var outcome = await SendWithFailoverAsync(
            message,
            provider,
            body,
            settings,
            attemptNumber: 0,
            request.SenderLine, // admin-selected line beats the provider default
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        return new SmsSendOutcome(
            outcome,
            message.Id,
            message.Status,
            message.ProviderMessageId,
            message.ErrorMessage,
            SmsTextUtilities.ToDisplayMobile(mobile));
    }

    public async Task<SmsBulkPreview> PreviewBulkAsync(
        SmsBulkPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        var (candidates, invalid) = await CollectRecipientsAsync(request, cancellationToken);
        var (final, duplicates, blocked) = await FilterRecipientsAsync(candidates, request.Kind, cancellationToken);

        var body = await ResolveTemplateBodyAsync(request.TemplateId, request.Body, cancellationToken);
        var rendered = SmsTextUtilities.RenderTemplate(body, SmsTextUtilities.SampleValues);
        var (segments, characters, isUnicode) = SmsTextUtilities.ComputeSegments(rendered);

        var provider = request.ProviderId is { } providerId
            ? await resolver.ResolveByIdAsync(providerId, cancellationToken)
            : await resolver.ResolveDefaultAsync(cancellationToken);

        var capabilities = await DescribeProviderAsync(provider, cancellationToken);

        return new SmsBulkPreview(
            candidates,
            invalid,
            duplicates,
            blocked,
            [],
            final.Count,
            final.Count * segments,
            rendered,
            segments,
            characters,
            isUnicode,
            capabilities);
    }

    public async Task<SmsCampaign> CreateCampaignAsync(
        SmsBulkPreviewRequest request,
        string title,
        DateTimeOffset? scheduledAt,
        string? senderLine,
        string? createdBy,
        CancellationToken cancellationToken = default)
    {
        var settings = appSetupStore.GetSmsSettings();
        if (!settings.BulkEnabled)
        {
            throw new InvalidOperationException("ارسال گروهی در تنظیمات پیامک غیرفعال شده است.");
        }

        var (candidates, invalid) = await CollectRecipientsAsync(request, cancellationToken);
        var (final, _, _) = await FilterRecipientsAsync(candidates, request.Kind, cancellationToken);

        if (final.Count == 0)
        {
            throw new InvalidOperationException("هیچ گیرندهٔ معتبری برای ارسال باقی نماند.");
        }

        if (final.Count > settings.MaxRecipientsPerSend)
        {
            throw new InvalidOperationException(
                $"تعداد گیرندگان ({final.Count}) از سقف مجاز هر ارسال ({settings.MaxRecipientsPerSend}) بیشتر است.");
        }

        if (await DailyLimitReachedAsync(final.Count, cancellationToken))
        {
            throw new InvalidOperationException("محدودیت ارسال روزانه اجازهٔ این حجم را نمی‌دهد.");
        }

        var body = await ResolveTemplateBodyAsync(request.TemplateId, request.Body, cancellationToken);
        if (string.IsNullOrWhiteSpace(body))
        {
            throw new InvalidOperationException("متن پیامک خالی است؛ قالب را انتخاب کنید یا متن بنویسید.");
        }

        var provider = request.ProviderId is { } providerId
            ? await resolver.ResolveByIdAsync(providerId, cancellationToken)
                ?? throw new InvalidOperationException("سرویس‌دهنده انتخاب‌شده پیدا نشد.")
            : await resolver.ResolveDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("هیچ سرویس‌دهنده فعالی تنظیم نشده است.");

        var rendered = SmsTextUtilities.RenderTemplate(body, SmsTextUtilities.SampleValues);
        // The chosen line is validated against THIS provider's lines; unknown
        // lines fall back to the provider default rather than failing at send time.
        var campaign = new SmsCampaign(
            title, rendered, request.Kind, request.TemplateId, provider.Id, final, createdBy,
            scheduledAt, CoalesceSenderLine(senderLine, provider));
        dbContext.SmsCampaigns.Add(campaign);

        var batchSize = Math.Max(1, settings.BatchSize);

        // One query for every contact variable used by the render pass.
        var contacts = await dbContext.SmsContacts
            .AsNoTracking()
            .Where(contact => final.Contains(contact.Mobile))
            .ToDictionaryAsync(contact => contact.Mobile, cancellationToken);

        for (var index = 0; index < final.Count; index++)
        {
            var recipientBody = RenderWithContact(body, final[index], contacts.GetValueOrDefault(final[index]));
            var message = new SmsMessage(
                provider.Id, final[index], recipientBody, request.Kind, SmsSendType.Bulk,
                request.TemplateId, campaign.Id, index / batchSize + 1, createdBy);
            message.MarkQueued();
            dbContext.SmsMessages.Add(message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "SMS campaign {Campaign} created: {Recipients} recipients in {Batches} batches.",
            campaign.Id, final.Count, (final.Count + batchSize - 1) / batchSize);
        return campaign;
    }

    public async Task<bool> SendSystemAsync(
        string? mobile,
        string message,
        string action,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalized = SmsTextUtilities.NormalizeMobile(mobile);
            if (normalized is null)
            {
                logger.LogWarning("System SMS ({Action}) skipped: invalid recipient {Mobile}.", action, mobile);
                return false;
            }

            var provider = await resolver.ResolveDefaultAsync(cancellationToken);
            if (provider is null)
            {
                // Let the caller (router) fall back to legacy appsettings behaviour.
                return false;
            }

            var dbMessage = new SmsMessage(
                provider.Id, normalized, message, SmsMessageKind.Service, SmsSendType.System,
                templateId: null, campaignId: null, batchNumber: 0, requestedBy: "system");
            dbContext.SmsMessages.Add(dbMessage);

            var gateway = gatewayFactory.Create(resolver.Decrypt(provider));
            var result = await gateway.SendAsync(normalized, message, null, cancellationToken);
            ApplyResult(dbMessage, result, provider, attemptNumber: 0);
            await RecordHealthAndLogAsync(provider, result, action, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return result.Success;
        }
        catch (Exception exception)
        {
            // System notifications are best-effort — never break business flows.
            logger.LogError(exception, "System SMS ({Action}) failed.", action);
            return false;
        }
    }

    public async Task<SmsSendOutcome> SendOtpAsync(
        string? mobile,
        string code,
        string? cafeName = null,
        string? recipientName = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = SmsTextUtilities.NormalizeMobile(mobile);
        if (normalized is null)
        {
            return new SmsSendOutcome(false, null, SmsSendStatus.Failed, null, "شماره موبایل معتبر نیست.", string.Empty);
        }

        var provider = await resolver.ResolveDefaultAsync(cancellationToken);
        if (provider is not { IsEnabled: true })
        {
            return new SmsSendOutcome(
                false,
                null,
                SmsSendStatus.Failed,
                null,
                "سرویس پیامک فعالی در پنل مدیریت تنظیم نشده است.",
                SmsTextUtilities.ToDisplayMobile(normalized));
        }

        var otpTemplate = await dbContext.SmsTemplates
            .AsNoTracking()
            .Include(t => t.ProviderMappings)
            .Where(t => t.IsActive && t.Kind == SmsMessageKind.Otp)
            .OrderByDescending(t => t.IsDefault)
            .ThenByDescending(t => t.LastModifiedAt ?? t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var brand = string.IsNullOrWhiteSpace(cafeName) ? "شوگریستا" : cafeName.Trim();
        var displayName = string.IsNullOrWhiteSpace(recipientName) ? "کاربر گرامی" : recipientName.Trim();
        var rawTemplate = !string.IsNullOrWhiteSpace(otpTemplate?.Body)
            ? otpTemplate.Body
            : $"کد تایید ورود به {brand}: {{Code}}\nاعتبار: ۲ دقیقه";

        var maskedBody = RenderOtpBody(rawTemplate, "••••••", displayName, brand);
        var actualBody = RenderOtpBody(rawTemplate, code, displayName, brand);

        var dbMessage = new SmsMessage(
            provider.Id,
            normalized,
            maskedBody,
            SmsMessageKind.Otp,
            SmsSendType.System,
            otpTemplate?.Id,
            campaignId: null,
            batchNumber: 0,
            requestedBy: "customer-otp");
        dbContext.SmsMessages.Add(dbMessage);

        var gateway = gatewayFactory.Create(resolver.Decrypt(provider));
        var mapping = otpTemplate?.ProviderMappings
            .FirstOrDefault(m => m.ProviderType == provider.ProviderType
                                 && !string.IsNullOrWhiteSpace(m.ProviderTemplateId));

        SmsSendResult result;
        if (gateway.SupportsPatternSend && mapping is not null)
        {
            var paramName = ResolveOtpPatternParamName(rawTemplate, provider.ProviderType);
            var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [paramName] = code
            };

            var nameParam = ResolveOtpPatternNameParamName(rawTemplate);
            if (!string.IsNullOrWhiteSpace(nameParam) && !parameters.ContainsKey(nameParam))
            {
                parameters[nameParam] = displayName;
            }

            result = await gateway.SendPatternAsync(
                normalized,
                mapping.ProviderTemplateId.Trim(),
                parameters,
                SenderLineFor(provider),
                cancellationToken);
        }
        else
        {
            result = await gateway.SendAsync(
                normalized,
                actualBody,
                SenderLineFor(provider),
                cancellationToken);
        }

        ApplyResult(dbMessage, result, provider, attemptNumber: 0);
        await RecordHealthAndLogAsync(provider, result, "customer-otp", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new SmsSendOutcome(
            result.Success,
            dbMessage.Id,
            dbMessage.Status,
            dbMessage.ProviderMessageId,
            dbMessage.ErrorMessage,
            SmsTextUtilities.ToDisplayMobile(normalized));
    }

    private static string RenderOtpBody(string template, string code, string recipientName, string brand)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Code"] = code,
            ["Otp"] = code,
            ["Token"] = code,
            ["Name"] = recipientName,
            ["CustomerName"] = recipientName,
            ["FullName"] = recipientName,
            ["Token2"] = recipientName,
            ["Cafe"] = brand,
            ["Brand"] = brand,
        };

        var rendered = SmsTextUtilities.RenderTemplate(template, values);
        // If the admin wrote a template without any placeholder, append the code cleanly.
        return rendered.Contains(code, StringComparison.Ordinal)
            ? rendered
            : $"{rendered.Trim()} {code}";
    }

    private static string? ResolveOtpPatternNameParamName(string templateBody)
    {
        var placeholders = SmsTextUtilities.ExtractPlaceholders(templateBody);
        return placeholders.FirstOrDefault(name =>
            name.Equals("name", StringComparison.OrdinalIgnoreCase)
            || name.Equals("customername", StringComparison.OrdinalIgnoreCase)
            || name.Equals("fullname", StringComparison.OrdinalIgnoreCase)
            || name.Equals("token2", StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveOtpPatternParamName(string templateBody, SmsProviderType providerType)
    {
        var placeholders = SmsTextUtilities.ExtractPlaceholders(templateBody);
        var match = placeholders.FirstOrDefault(name =>
            name.Equals("code", StringComparison.OrdinalIgnoreCase)
            || name.Equals("otp", StringComparison.OrdinalIgnoreCase)
            || name.Equals("token", StringComparison.OrdinalIgnoreCase)
            || name.Equals("verificationcode", StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(match))
        {
            return match;
        }

        return providerType switch
        {
            SmsProviderType.SmsIr => "CODE",
            SmsProviderType.Kavenegar => "token",
            _ => "code",
        };
    }

    // ---- internals -------------------------------------------------------

    /// <summary>One send including (policy-safe) failover attempt. Saves health/log internally.</summary>
    private async Task<bool> SendWithFailoverAsync(
        SmsMessage message,
        SmsProvider primary,
        string body,
        SmsGlobalSettings settings,
        int attemptNumber,
        string? requestedSenderLine,
        CancellationToken cancellationToken)
    {
        message.AttachAttempt(primary.Id, attemptNumber);
        var gateway = gatewayFactory.Create(resolver.Decrypt(primary));
        var result = await gateway.SendAsync(
            message.Mobile,
            body,
            CoalesceSenderLine(requestedSenderLine, primary),
            cancellationToken);
        ApplyResult(message, result, primary, attemptNumber);
        await RecordHealthAndLogAsync(primary, result, "send", cancellationToken);

        if (result.Success || settings.FailoverEnabled is false || attemptNumber > 0)
        {
            return result.Success;
        }

        // Failover policy:
        //  • ambiguous timeout → NEVER (might have been sent; duplicate risk),
        //  • REJECTED → NEVER (provider-side validation: number blocked/blacklist),
        //  • plain Failed (connection/HTTP errors) → allowed once.
        if (SmsSendErrors.IsAmbiguousTimeout(result.Error) || result.Status == SmsOutcomeStatus.Rejected)
        {
            return false;
        }

        var failoverProvider = await resolver.ResolveFailoverAsync(primary.Id, cancellationToken);
        if (failoverProvider is null)
        {
            return false;
        }

        logger.LogInformation(
            "SMS failover: primary {Primary} failed ({Error}); retrying via {Failover}.",
            primary.Name, result.Error, failoverProvider.Name);

        return await SendWithFailoverAsync(message, failoverProvider, body, settings, attemptNumber + 1, null, cancellationToken);
    }

    private void ApplyResult(SmsMessage message, SmsSendResult result, SmsProvider provider, int attemptNumber)
    {
        var status = result.Success
            ? result.Status == SmsOutcomeStatus.Accepted ? SmsSendStatus.Accepted : SmsSendStatus.Sent
            : result.Status == SmsOutcomeStatus.Rejected
                ? SmsSendStatus.Rejected
                : SmsSendErrors.IsAmbiguousTimeout(result.Error)
                    ? SmsSendStatus.Unknown
                    : SmsSendStatus.Failed;

        message.MarkResult(
            status,
            result.ProviderMessageId,
            result.ProviderResponse,
            result.Cost,
            result.Error);
    }

    private async Task RecordHealthAndLogAsync(
        SmsProvider provider,
        SmsSendResult result,
        string action,
        CancellationToken cancellationToken)
    {
        if (result.Success)
        {
            provider.MarkConnectionSucceeded(DateTimeOffset.UtcNow);
        }
        else if (!SmsSendErrors.IsAmbiguousTimeout(result.Error))
        {
            provider.MarkConnectionFailed(result.Error ?? "خطای نامشخص", DateTimeOffset.UtcNow);
        }

        await resolver.RecordLogAsync(provider, action, result.Success, result.Error ?? result.ProviderResponse, cancellationToken);
    }

    private static string? SenderLineFor(SmsProvider provider) =>
        provider.SenderLines.FirstOrDefault(line => line.IsDefaultLine)?.Number
        ?? provider.SenderLines.FirstOrDefault()?.Number;

    /// <summary>An admin-selected line only applies to the provider it was chosen for.</summary>
    private static string? CoalesceSenderLine(string? requestedSenderLine, SmsProvider provider) =>
        string.IsNullOrWhiteSpace(requestedSenderLine)
            ? SenderLineFor(provider)
            : provider.SenderLines.Any(line => line.Number == requestedSenderLine)
                ? requestedSenderLine
                : SenderLineFor(provider);

    private async Task<string> ResolveBodyAsync(
        SmsSendRequest request,
        string normalizedMobile,
        CancellationToken cancellationToken)
    {
        var body = await ResolveTemplateBodyAsync(request.TemplateId, request.Body, cancellationToken);
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        return await RenderForRecipient(body, normalizedMobile, cancellationToken);
    }

    private async Task<string> ResolveTemplateBodyAsync(
        Guid? templateId,
        string? fallbackBody,
        CancellationToken cancellationToken)
    {
        if (templateId is { } id)
        {
            var template = await dbContext.SmsTemplates.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
            if (template is not { IsActive: true })
            {
                return string.Empty;
            }

            return template.Body;
        }

        return fallbackBody ?? string.Empty;
    }

    /// <summary>Fills contact variables when the recipient is in the address book; unknown placeholders stay visible.</summary>
    private async Task<string> RenderForRecipient(string body, string normalizedMobile, CancellationToken cancellationToken)
    {
        var contact = await dbContext.SmsContacts
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Mobile == normalizedMobile, cancellationToken);
        return RenderWithContact(body, normalizedMobile, contact);
    }

    private static string RenderWithContact(string body, string normalizedMobile, SmsContact? contact)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Mobile"] = SmsTextUtilities.ToDisplayMobile(normalizedMobile)
        };
        if (contact is not null)
        {
            values["FirstName"] = contact.FirstName ?? string.Empty;
            values["LastName"] = contact.LastName ?? string.Empty;
        }

        return SmsTextUtilities.RenderTemplate(body, values);
    }

    private async Task<bool> DailyLimitReachedAsync(int additionalMessages, CancellationToken cancellationToken)
    {
        var settings = appSetupStore.GetSmsSettings();
        // Npgsql timestamptz only accepts UTC offset 0.
        var startOfDayUtc = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
        var sentToday = await dbContext.SmsMessages
            .CountAsync(m => m.RequestedAt >= startOfDayUtc, cancellationToken);
        return sentToday + additionalMessages > settings.DailyMessageLimit;
    }

    /// <summary>Raw recipient pool: manual list + all members of the selected groups.</summary>
    private async Task<(List<string> Candidates, List<string> Invalid)> CollectRecipientsAsync(
        SmsBulkPreviewRequest request,
        CancellationToken cancellationToken)
    {
        var candidates = new List<string>();
        var invalid = new List<string>();

        foreach (var raw in request.Recipients)
        {
            var normalized = SmsTextUtilities.NormalizeMobile(raw);
            if (normalized is null)
            {
                invalid.Add(raw);
            }
            else
            {
                candidates.Add(normalized);
            }
        }

        if (request.GroupIds.Count > 0)
        {
            var groups = await dbContext.SmsContactGroups
                .AsNoTracking()
                .Where(group => request.GroupIds.Contains(group.Id))
                .ToListAsync(cancellationToken);

            var groupIds = groups.Select(group => group.Id).ToList();

            // Ordinary groups: fixed membership table.
            var memberMobiles = await dbContext.SmsContactGroupMembers
                .AsNoTracking()
                .Where(member => groupIds.Contains(member.GroupId))
                .Join(dbContext.SmsContacts,
                    member => member.ContactId,
                    contact => contact.Id,
                    (member, contact) => contact.Mobile)
                .ToListAsync(cancellationToken);
            candidates.AddRange(memberMobiles);

            // Smart groups: membership is computed from the filter at send time.
            foreach (var group in groups.Where(item => item.IsSmart))
            {
                var filter = SmsSmartContactFilter.FromJsonOrNull(group.SmartFilterJson)
                             ?? SmsSmartContactFilter.Default;
                if (filter.IsEmpty)
                {
                    // Empty smart filter is treated as "all active contacts".
                    filter = filter with { IsActive = true };
                }

                var now = DateTimeOffset.UtcNow;
                var cutoff = filter.AddedWithinDays is > 0
                    ? new DateTimeOffset(now.DateTime.Date.AddDays(-filter.AddedWithinDays.Value), TimeSpan.Zero)
                    : (DateTimeOffset?)null;
                var smartMobiles = await dbContext.SmsContacts
                    .AsNoTracking()
                    .Where(contact =>
                        (filter.IsActive == null || contact.IsActive == filter.IsActive)
                        && (filter.MarketingConsent == null || contact.MarketingConsent == filter.MarketingConsent)
                        && (cutoff == null || contact.CreatedAt >= cutoff))
                    .Select(contact => contact.Mobile)
                    .ToListAsync(cancellationToken);
                candidates.AddRange(smartMobiles);
            }
        }

        return (candidates, invalid);
    }

    /// <summary>Dedupe → opt-out / no-consent (marketing) → inactive contacts.</summary>
    private async Task<(List<string> Final, List<string> Duplicates, List<string> Blocked)> FilterRecipientsAsync(
        List<string> candidates,
        SmsMessageKind kind,
        CancellationToken cancellationToken)
    {
        var duplicates = new List<string>();
        var seen = new HashSet<string>();
        var unique = new List<string>();
        foreach (var mobile in candidates)
        {
            if (seen.Add(mobile))
            {
                unique.Add(mobile);
            }
            else
            {
                duplicates.Add(mobile);
            }
        }

        // Opt-out list (blocked numbers) + marketing consent, loaded in bulk.
        var optedOut = await dbContext.SmsOptOuts
            .AsNoTracking()
            .Select(optOut => optOut.Mobile)
            .ToListAsync(cancellationToken);
        var optOutSet = optedOut.ToHashSet();

        var blocked = unique.Where(optOutSet.Contains).ToList();
        var pool = unique.Where(mobile => !optOutSet.Contains(mobile)).ToList();

        if (kind == SmsMessageKind.Marketing)
        {
            var consentStates = await dbContext.SmsContacts
                .AsNoTracking()
                .Where(contact => pool.Contains(contact.Mobile))
                .Select(contact => new { contact.Mobile, contact.MarketingConsent })
                .ToDictionaryAsync(entry => entry.Mobile, entry => entry.MarketingConsent, cancellationToken);

            blocked.AddRange(pool.Where(mobile => consentStates.TryGetValue(mobile, out var consent) && !consent));
            pool = pool.Where(mobile => consentStates.TryGetValue(mobile, out var consent) && consent).ToList();
        }

        // Inactive contacts are excluded from every send.
        var activeStates = await dbContext.SmsContacts
            .AsNoTracking()
            .Where(contact => pool.Contains(contact.Mobile))
            .Select(contact => new { contact.Mobile, contact.IsActive })
            .ToDictionaryAsync(entry => entry.Mobile, entry => entry.IsActive, cancellationToken);
        blocked.AddRange(pool.Where(mobile => activeStates.TryGetValue(mobile, out var active) && !active));
        var final = pool.Where(mobile => !activeStates.TryGetValue(mobile, out var active) || active).ToList();

        return (final, duplicates, blocked);
    }

    private async Task<SmsProviderCapabilities> DescribeProviderAsync(
        SmsProvider? provider,
        CancellationToken cancellationToken)
    {
        if (provider is null)
        {
            return new SmsProviderCapabilities("—", false, 0, false, null);
        }

        var gateway = gatewayFactory.Create(resolver.Decrypt(provider));
        decimal? credit = null;
        if (gateway.SupportsCreditCheck)
        {
            var creditResult = await gateway.GetCreditAsync(cancellationToken);
            credit = creditResult.Success ? creditResult.Credit : null;
        }

        return new SmsProviderCapabilities(
            provider.Name,
            gateway.SupportsBulkSend,
            gateway.BulkBatchSize,
            gateway.SupportsCreditCheck,
            credit);
    }
}
