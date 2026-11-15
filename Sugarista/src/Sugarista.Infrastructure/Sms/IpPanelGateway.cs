using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sugarista.Application.Sms;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Sms;

/// <summary>
/// IPPanel (ippanel.com / فراپنل) adapter: REST v1, "ApiKey" auth header,
/// JSON bodies. The single-send endpoint accepts a mobile list, so bulk is
/// native (the response does not carry per-number ids — the runner fills them).
/// </summary>
public sealed class IpPanelGateway(
    HttpClient httpClient,
    ILogger<IpPanelGateway> logger,
    string? apiKey,
    string? baseUrl) : ISmsProviderGateway
{
    private const string DefaultBase = "https://api.ippanel.com/api/v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private string Base => string.IsNullOrWhiteSpace(baseUrl) ? DefaultBase : baseUrl.TrimEnd('/');

    public SmsProviderType ProviderType => SmsProviderType.IpPanel;

    public bool SupportsSingleSend => true;
    public bool SupportsBulkSend => true;
    public bool SupportsPatternSend => true;
    public bool SupportsCreditCheck => true;
    public bool SupportsMessageStatus => true;
    public bool SupportsDeliveryReport => true;
    public int BulkBatchSize => 100;

    public async Task<SmsSendResult> SendAsync(
        string mobile,
        string message,
        string? senderLine,
        CancellationToken cancellationToken = default)
    {
        var bulk = await SendBulkAsync([mobile], message, senderLine, cancellationToken);
        if (!bulk.Success)
        {
            return SmsSendResult.Failed(bulk.Error ?? "ارسال ناموفق بود.");
        }

        var item = bulk.Items.FirstOrDefault();
        return item is { Success: true }
            ? SmsSendResult.Ok(item.ProviderMessageId, null, null)
            : SmsSendResult.Failed(item?.Error ?? "ارسال ناموفق بود.");
    }

    public async Task<SmsBulkResult> SendBulkAsync(
        IReadOnlyList<string> mobiles,
        string message,
        string? senderLine,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return SmsBulkResult.Failed("کلید API آی‌پنل تنظیم نشده است.");
        }

        if (string.IsNullOrWhiteSpace(senderLine))
        {
            return SmsBulkResult.Failed("خط ارسال ( line_number ) برای آی‌پنل الزامی است.");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{Base}/sms/send/webrtc/single");
            request.Headers.Authorization = new AuthenticationHeaderValue("ApiKey", apiKey);
            request.Content = JsonContent.Create(new
            {
                line_number = senderLine,
                text = message,
                mobiles = mobiles.ToArray(),
            });

            using var response = await httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("IPPanel send failed ({Code}): {Body}", (int)response.StatusCode, body);
                return SmsBulkResult.Failed($"آی‌پنل درخواست را نپذیرفت (کد {(int)response.StatusCode}).");
            }

            var payload = JsonSerializer.Deserialize<IpPanelEnvelope>(body, JsonOptions);
            if (!string.Equals(payload?.Status, "OK", StringComparison.OrdinalIgnoreCase))
            {
                return SmsBulkResult.Failed($"آی‌پنل: {payload?.Message ?? "خطای نامشخص"}");
            }

            var messageId = payload!.Data?.MessageId?.ToString();
            var cost = payload.Data?.Price is > 0 ? payload.Data.Price : null;
            return SmsBulkResult.Ok(
                mobiles.Select(mobile => new SmsBulkItemResult(mobile, true, null, null)).ToList(),
                messageId,
                cost);
        }
        catch (TaskCanceledException)
        {
            return SmsBulkResult.Failed(SmsSendErrors.AmbiguousTimeout);
        }
        catch (HttpRequestException)
        {
            return SmsBulkResult.Failed("ارتباط با آی‌پنل برقرار نشد.");
        }
    }

    public async Task<SmsSendResult> SendPatternAsync(
        string mobile,
        string providerTemplateId,
        IReadOnlyDictionary<string, string> values,
        string? senderLine,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return SmsSendResult.Failed("کلید API آی‌پنل تنظیم نشده است.");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{Base}/sms/send/interactive");
            request.Headers.Authorization = new AuthenticationHeaderValue("ApiKey", apiKey);
            request.Content = JsonContent.Create(new
            {
                line_number = senderLine,
                text = values.Values.ToArray(),
                mobiles = new[] { mobile },
                template_name = providerTemplateId,
            });

            using var response = await httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var payload = JsonSerializer.Deserialize<IpPanelEnvelope>(body, JsonOptions);
            if (!string.Equals(payload?.Status, "OK", StringComparison.OrdinalIgnoreCase))
            {
                return SmsSendResult.Failed($"آی‌پنل Pattern: {payload?.Message ?? "خطای نامشخص"}");
            }

            return SmsSendResult.Ok(payload!.Data?.MessageId?.ToString(), null, null);
        }
        catch (TaskCanceledException)
        {
            return SmsSendResult.Failed(SmsSendErrors.AmbiguousTimeout);
        }
        catch (HttpRequestException)
        {
            return SmsSendResult.Failed("ارتباط با آی‌پنل برقرار نشد.");
        }
    }

    public async Task<SmsCreditResult> GetCreditAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new SmsCreditResult(false, null, "کلید API تنظیم نشده است.");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{Base}/sms/account/info");
            request.Headers.Authorization = new AuthenticationHeaderValue("ApiKey", apiKey);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var payload = JsonSerializer.Deserialize<IpPanelEnvelope>(body, JsonOptions);
            if (!string.Equals(payload?.Status, "OK", StringComparison.OrdinalIgnoreCase))
            {
                return new SmsCreditResult(false, null, $"آی‌پنل: {payload?.Message ?? "خطای نامشخص"}");
            }

            return payload!.Data?.Credit is { } credit
                ? new SmsCreditResult(true, credit, null)
                : new SmsCreditResult(false, null, "اعتبار قابل خواندن نبود.");
        }
        catch (TaskCanceledException)
        {
            return new SmsCreditResult(false, null, SmsSendErrors.AmbiguousTimeout);
        }
        catch (HttpRequestException)
        {
            return new SmsCreditResult(false, null, "ارتباط با آی‌پنل برقرار نشد.");
        }
    }

    public Task<SmsMessageStatusResult> GetMessageStatusAsync(
        string providerMessageId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new SmsMessageStatusResult(
            false,
            SmsOutcomeStatus.Unknown,
            null,
            "وضعیت آی‌پنل فقط با Webhook گزارش تحویل به‌روزرسانی می‌شود."));

    public async Task<SmsConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var credit = await GetCreditAsync(cancellationToken);
        return credit.Success
            ? new SmsConnectionTestResult(true, "اتصال به آی‌پنل برقرار است.", credit.Credit)
            : new SmsConnectionTestResult(false, credit.Error ?? "اتصال برقرار نشد.", null);
    }

    private sealed class IpPanelEnvelope
    {
        public string? Status { get; set; }
        public string? Message { get; set; }
        public IpPanelData? Data { get; set; }
    }

    private sealed class IpPanelData
    {
        public long? MessageId { get; set; }
        public decimal? Price { get; set; }
        public decimal? Credit { get; set; }
    }
}
