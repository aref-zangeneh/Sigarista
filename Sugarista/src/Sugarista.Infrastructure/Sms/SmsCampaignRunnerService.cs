using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Sms;
using Sugarista.Domain.Entities;
using Sugarista.Infrastructure.Data.DbConnection;
using Sugarista.Infrastructure.Services;
using Sugarista.Infrastructure.Settings;

namespace Sugarista.Infrastructure.Sms;

/// <summary>
/// Single-worker background service that drains bulk campaigns. Rest calls
/// only create the campaign rows; this runner does the provider calls in
/// configurable batches, honours cancellation, applies a LIMITED retry for
/// transient provider failures and NEVER retries ambiguous timeouts.
/// Mirrors ExportJobRunnerService's database gate discipline.
/// </summary>
public sealed class SmsCampaignRunnerService(
    DbConnectionManager connectionManager,
    IServiceScopeFactory scopeFactory,
    ILogger<SmsCampaignRunnerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await BackgroundDatabaseGate.WaitUntilConfiguredAsync(connectionManager, logger, nameof(SmsCampaignRunnerService), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await ProcessNextCampaignAsync(stoppingToken);
                if (!processed)
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "SMS campaign runner iteration failed; retrying shortly.");
                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            }
        }
    }

    /// <summary>Returns true when it did work this tick (no idle delay).</summary>
    private async Task<bool> ProcessNextCampaignAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var resolver = scope.ServiceProvider.GetRequiredService<ISmsProviderResolver>();
        var gatewayFactory = scope.ServiceProvider.GetRequiredService<ISmsProviderGatewayFactory>();
        var appSetupStore = scope.ServiceProvider.GetRequiredService<AppSetupStore>();

        var campaign = await dbContext.SmsCampaigns
            .Where(item => (item.Status == SmsCampaignStatus.Pending || item.Status == SmsCampaignStatus.Processing)
                           // Scheduled campaigns are picked up only when due.
                           && (item.ScheduledAt == null || item.ScheduledAt <= DateTimeOffset.UtcNow))
            .OrderBy(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (campaign is null)
        {
            return false;
        }

        var provider = await resolver.ResolveByIdAsync(campaign.ProviderId, cancellationToken);
        if (provider is not { IsEnabled: true })
        {
            campaign.MarkFailed(DateTimeOffset.UtcNow);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogWarning("SMS campaign {Campaign} failed: its provider is missing or disabled.", campaign.Id);
            return true;
        }

        campaign.MarkProcessing(DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        var gateway = gatewayFactory.Create(resolver.Decrypt(provider));
        var batchSize = Math.Max(gateway.BulkBatchSize, 1);
        var settings = appSetupStore.GetSmsSettings();

        var sentCount = 0;
        var failedCount = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (campaign.CancelRequested)
            {
                campaign.MarkCancelled(DateTimeOffset.UtcNow);
                await dbContext.SaveChangesAsync(cancellationToken);
                logger.LogInformation("SMS campaign {Campaign} cancelled at admin request.", campaign.Id);
                return true;
            }

            var messages = await dbContext.SmsMessages
                .Where(message => message.CampaignId == campaign.Id
                                  && (message.Status == SmsSendStatus.Queued || message.Status == SmsSendStatus.Pending))
                .OrderBy(message => message.Id)
                .Take(batchSize)
                .ToListAsync(cancellationToken);
            if (messages.Count == 0)
            {
                break;
            }

            var batchBody = messages[0].Body;
            var mobiles = messages.Select(message => message.Mobile).ToList();

            foreach (var message in messages)
            {
                message.MarkProcessing();
            }
            await dbContext.SaveChangesAsync(cancellationToken);

            var results = new List<SmsBulkItemResult>();

            if (gateway.SupportsBulkSend)
            {
                var bulk = await gateway.SendBulkAsync(mobiles, batchBody, SenderLineFor(provider, campaign.SenderLine), cancellationToken);
                if (bulk.Success)
                {
                    results.AddRange(bulk.Items);
                    if (results.Count < mobiles.Count)
                    {
                        // Provider confirmed the batch but skipped item details.
                        results.AddRange(mobiles.Skip(results.Count)
                            .Select(mobile => new SmsBulkItemResult(mobile, true, null, null)));
                    }

                    foreach (var message in messages)
                    {
                        var item = results.FirstOrDefault(entry => entry.Mobile == message.Mobile)
                                   ?? new SmsBulkItemResult(message.Mobile, true, null, null);
                        ApplyResult(
                            message,
                            item.Success
                                ? SmsSendResult.Ok(item.ProviderMessageId, null, null)
                                : SmsSendResult.Failed(item.Error ?? "خطای نامشخص Provider."));
                    }

                    await RecordHealthAndLogAsync(
                        provider,
                        SmsSendResult.Ok(null, "bulk", null),
                        $"campaign:{campaign.Id:N}",
                        resolver,
                        cancellationToken);
                }
                else if (SmsSendErrors.IsAmbiguousTimeout(bulk.Error))
                {
                    // Ambiguous: the batch MIGHT have gone out. Never resend —
                    // park every row as Unknown for manual inspection.
                    foreach (var message in messages)
                    {
                        message.MarkResult(SmsSendStatus.Unknown, null, null, null, bulk.Error);
                    }

                    await RecordHealthAndLogAsync(
                        provider,
                        SmsSendResult.Failed(bulk.Error ?? SmsSendErrors.AmbiguousTimeout),
                        $"campaign:{campaign.Id:N}",
                        resolver,
                        cancellationToken);
                }
                else
                {
                    // Native bulk failed for the WHOLE batch → fall back to
                    // per-recipient single sends with the retry policy.
                    await SendEachWithRetryAsync(messages, provider, gateway, settings, campaign, resolver, cancellationToken);
                }
            }
            else
            {
                await SendEachWithRetryAsync(messages, provider, gateway, settings, campaign, resolver, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            sentCount += messages.Count(message => message.Status is SmsSendStatus.Accepted or SmsSendStatus.Sent);
            failedCount += messages.Count(message => message.Status is SmsSendStatus.Failed or SmsSendStatus.Rejected);
            campaign.UpdateProgress(sentCount, failedCount);
            await dbContext.SaveChangesAsync(cancellationToken);

            // Rate-limit discipline between provider calls.
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, settings.RetryDelaySeconds)), cancellationToken);
        }

        campaign.UpdateProgress(sentCount, failedCount);
        campaign.MarkCompleted(DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "SMS campaign {Campaign} finished: {Sent} sent, {Failed} failed.",
            campaign.Id, sentCount, failedCount);
        return true;
    }

    /// <summary>Per-recipient send with the LIMITED retry policy: transient failures
    /// retry up to MaxRetries; REJECTED and ambiguous-timeout results never do.</summary>
    private async Task SendEachWithRetryAsync(
        List<SmsMessage> messages,
        SmsProvider provider,
        ISmsProviderGateway gateway,
        SmsGlobalSettings settings,
        SmsCampaign campaign,
        ISmsProviderResolver resolver,
        CancellationToken cancellationToken)
    {
        foreach (var message in messages)
        {
            for (var attempt = 0; ; attempt++)
            {
                var result = await gateway.SendAsync(message.Mobile, message.Body, SenderLineFor(provider, campaign.SenderLine), cancellationToken);

                var transientFailure = result.Success is false
                                       && result.Status != SmsOutcomeStatus.Rejected
                                       && !SmsSendErrors.IsAmbiguousTimeout(result.Error);

                if (!transientFailure || attempt >= settings.MaxRetries)
                {
                    ApplyResult(message, result);
                    await RecordHealthAndLogAsync(provider, result, $"campaign:{campaign.Id:N}", resolver, cancellationToken);
                    break;
                }

                logger.LogWarning(
                    "SMS transient failure for {Mobile} (attempt {Attempt}/{Max}): {Error}.",
                    message.Mobile, attempt + 1, settings.MaxRetries, result.Error);
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, settings.RetryDelaySeconds)), cancellationToken);
            }
        }
    }

    private void ApplyResult(SmsMessage message, SmsSendResult result)
    {
        var status = result.Success
            ? result.Status == SmsOutcomeStatus.Accepted ? SmsSendStatus.Accepted : SmsSendStatus.Sent
            : result.Status == SmsOutcomeStatus.Rejected
                ? SmsSendStatus.Rejected
                : SmsSendErrors.IsAmbiguousTimeout(result.Error)
                    ? SmsSendStatus.Unknown
                    : SmsSendStatus.Failed;

        message.MarkResult(status, result.ProviderMessageId, result.ProviderResponse, result.Cost, result.Error);
    }

    private async Task RecordHealthAndLogAsync(
        SmsProvider provider,
        SmsSendResult result,
        string action,
        ISmsProviderResolver resolver,
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

    /// <summary>The admin-selected line (validated at creation) beats the provider default.</summary>
    private static string? SenderLineFor(SmsProvider provider, string? campaignLine) =>
        string.IsNullOrWhiteSpace(campaignLine)
            ? provider.SenderLines.FirstOrDefault(line => line.IsDefaultLine)?.Number
              ?? provider.SenderLines.FirstOrDefault()?.Number
            : provider.SenderLines.Any(line => line.Number == campaignLine)
                ? campaignLine
                : provider.SenderLines.FirstOrDefault(line => line.IsDefaultLine)?.Number
                  ?? provider.SenderLines.FirstOrDefault()?.Number;
}
