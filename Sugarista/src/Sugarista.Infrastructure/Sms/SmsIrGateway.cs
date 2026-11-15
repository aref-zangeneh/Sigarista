using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sugarista.Application.Sms;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Sms;

/// <summary>
/// SMS.ir adapter (REST v1, x-api-key header, JSON bodies). Bulk send is
/// native: one call carries up to N mobiles and returns a batch messageId.
/// </summary>
public sealed class SmsIrGateway(
    HttpClient httpClient,
    ILogger<SmsIrGateway> logger,
    string? apiKey,
    string? baseUrl) : ISmsProviderGateway
{
    private const string DefaultBase = "https://api.sms.ir/v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private string Base => string.IsNullOrWhiteSpace(baseUrl) ? DefaultBase : baseUrl.TrimEnd('/');

    public SmsProviderType ProviderType => SmsProviderType.SmsIr;

    public bool SupportsSingleSend => true;
    public bool SupportsBulkSend => true;
    public bool SupportsPatternSend => true;
    public bool SupportsCreditCheck => true;
    public bool SupportsMessageStatus => false; // DR webhook only
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
            ? SmsSendResult.Ok(item.ProviderMessageId, null, bulk.Cost)
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
            return SmsBulkResult.Failed("کلید API سرویس SMS.ir تنظیم نشده است.");
        }

        if (string.IsNullOrWhiteSpace(senderLine))
        {
            return SmsBulkResult.Failed("خط ارسال ( lineNumber ) تنظیم نشده است.");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{Base}/send/bulk");
            request.Headers.Add("x-api-key", apiKey);
            request.Content = JsonContent.Create(new
            {
                lineNumber = senderLine,
                messageText = message,
                mobiles = mobiles.ToArray()
            });

            using var response = await httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("SMS.ir send failed ({Code}): {Body}", (int)response.StatusCode, body);
                return SmsBulkResult.Failed($"SMS.ir درخواست را نپذیرفت (کد {(int)response.StatusCode}).");
            }

            var payload = JsonSerializer.Deserialize<SmsIrEnvelope>(body, JsonOptions);
            if (payload?.Status is not 1)
            {
                return SmsBulkResult.Failed($"SMS.ir: {payload?.Message ?? "خطای نامشخص"}");
            }

            var cost = payload.Data?.Cost is > 0 ? payload.Data.Cost / 10m : (decimal?)null; // Rial → Toman
            return SmsBulkResult.Ok(
                mobiles.Select(mobile => new SmsBulkItemResult(mobile, true, null, null)).ToList(),
                payload.Data?.MessageId?.ToString(),
                cost);
        }
        catch (TaskCanceledException)
        {
            return SmsBulkResult.Failed(SmsSendErrors.AmbiguousTimeout);
        }
        catch (HttpRequestException)
        {
            return SmsBulkResult.Failed("ارتباط با SMS.ir برقرار نشد.");
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
            return SmsSendResult.Failed("کلید API سرویس SMS.ir تنظیم نشده است.");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{Base}/send/verify");
            request.Headers.Add("x-api-key", apiKey);
            request.Content = JsonContent.Create(new
            {
                mobile = mobile,
                templateId = providerTemplateId,
                parameters = values.Select(pair => new { name = pair.Key, value = pair.Value }).ToArray()
            });

            using var response = await httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var payload = JsonSerializer.Deserialize<SmsIrEnvelope>(body, JsonOptions);
            if (payload?.Status is not 1)
            {
                return SmsSendResult.Failed($"SMS.ir: {payload?.Message ?? "خطای نامشخص"}");
            }

            return SmsSendResult.Ok(payload.Data?.MessageId?.ToString(), null, null);
        }
        catch (TaskCanceledException)
        {
            return SmsSendResult.Failed(SmsSendErrors.AmbiguousTimeout);
        }
        catch (HttpRequestException)
        {
            return SmsSendResult.Failed("ارتباط با SMS.ir برقرار نشد.");
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
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{Base}/credit");
            request.Headers.Add("x-api-key", apiKey);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var payload = JsonSerializer.Deserialize<SmsIrEnvelope>(body, JsonOptions);
            if (payload?.Status is not 1)
            {
                return new SmsCreditResult(false, null, $"SMS.ir: {payload?.Message ?? "خطای نامشخص"}");
            }

            return new SmsCreditResult(true, (decimal?)payload.Data?.Credit, null);
        }
        catch (TaskCanceledException)
        {
            return new SmsCreditResult(false, null, SmsSendErrors.AmbiguousTimeout);
        }
        catch (HttpRequestException)
        {
            return new SmsCreditResult(false, null, "ارتباط با SMS.ir برقرار نشد.");
        }
    }

    public Task<SmsMessageStatusResult> GetMessageStatusAsync(
        string providerMessageId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new SmsMessageStatusResult(
            false,
            SmsOutcomeStatus.Unknown,
            null,
            "این Provider وضعیت را فقط با Webhook گزارش می‌کند."));

    public async Task<SmsConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var credit = await GetCreditAsync(cancellationToken);
        return credit.Success
            ? new SmsConnectionTestResult(true, "اتصال به SMS.ir برقرار است.", credit.Credit)
            : new SmsConnectionTestResult(false, credit.Error ?? "اتصال برقرار نشد.", null);
    }

    private sealed class SmsIrEnvelope
    {
        public int? Status { get; set; }
        public string? Message { get; set; }
        public SmsIrData? Data { get; set; }
    }

    private sealed class SmsIrData
    {
        public long? MessageId { get; set; }
        public long? Cost { get; set; }
        public decimal? Credit { get; set; }
    }
}
