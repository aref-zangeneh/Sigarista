using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sugarista.Application.Sms;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Sms;

/// <summary>
/// Ghasedak adapter (api.ghasedak.me REST v2, apikey header, form bodies).
/// The simple-send endpoint reports one message id per call, so bulk is
/// executed by the runner as per-recipient single sends.
/// </summary>
public sealed class GhasedakGateway(
    HttpClient httpClient,
    ILogger<GhasedakGateway> logger,
    string? apiKey,
    string? baseUrl) : ISmsProviderGateway
{
    private const string DefaultBase = "https://api.ghasedak.me/v2";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private string Base => string.IsNullOrWhiteSpace(baseUrl) ? DefaultBase : baseUrl.TrimEnd('/');

    public SmsProviderType ProviderType => SmsProviderType.Ghasedak;

    public bool SupportsSingleSend => true;
    public bool SupportsBulkSend => false;
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
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return SmsSendResult.Failed("کلید API قاصدک تنظیم نشده است.");
        }

        if (string.IsNullOrWhiteSpace(senderLine))
        {
            return SmsSendResult.Failed("خط ارسال ( linenumber ) برای قاصدک الزامی است.");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{Base}/message/simple/send");
            request.Headers.Authorization = new AuthenticationHeaderValue("apikey", apiKey);
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["message"] = message,
                ["receptor"] = mobile,
                ["linenumber"] = senderLine,
            });

            using var response = await httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Ghasedak send failed ({Code}): {Body}", (int)response.StatusCode, body);
                return SmsSendResult.Failed($"قاصدک درخواست را نپذیرفت (کد {(int)response.StatusCode}).");
            }

            var payload = JsonSerializer.Deserialize<GhasedakEnvelope>(body, JsonOptions);
            if (!string.Equals(payload?.Result, "success", StringComparison.OrdinalIgnoreCase))
            {
                return SmsSendResult.Failed($"قاصدک: {payload?.Message ?? "خطای نامشخص"}");
            }

            return SmsSendResult.Ok(
                payload!.Items?.FirstOrDefault()?.MessageId?.ToString(),
                null,
                null);
        }
        catch (TaskCanceledException)
        {
            return SmsSendResult.Failed(SmsSendErrors.AmbiguousTimeout);
        }
        catch (HttpRequestException)
        {
            return SmsSendResult.Failed("ارتباط با قاصدک برقرار نشد.");
        }
    }

    public Task<SmsBulkResult> SendBulkAsync(
        IReadOnlyList<string> mobiles,
        string message,
        string? senderLine,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(SmsBulkResult.Failed("این Provider ارسال گروهی بومی ندارد."));

    public async Task<SmsSendResult> SendPatternAsync(
        string mobile,
        string providerTemplateId,
        IReadOnlyDictionary<string, string> values,
        string? senderLine,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return SmsSendResult.Failed("کلید API قاصدک تنظیم نشده است.");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{Base}/verification/send/simple");
            request.Headers.Authorization = new AuthenticationHeaderValue("apikey", apiKey);
            // Ghasedak's simple verification carries a single template parameter.
            var parameter = values.Values.FirstOrDefault() ?? string.Empty;
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["receptor"] = mobile,
                ["template"] = providerTemplateId,
                ["param1"] = parameter,
                ["type"] = "1",
            });

            using var response = await httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var payload = JsonSerializer.Deserialize<GhasedakEnvelope>(body, JsonOptions);
            if (!string.Equals(payload?.Result, "success", StringComparison.OrdinalIgnoreCase))
            {
                return SmsSendResult.Failed($"قاصدک Verify: {payload?.Message ?? "خطای نامشخص"}");
            }

            return SmsSendResult.Ok(payload!.Items?.FirstOrDefault()?.MessageId?.ToString(), null, null);
        }
        catch (TaskCanceledException)
        {
            return SmsSendResult.Failed(SmsSendErrors.AmbiguousTimeout);
        }
        catch (HttpRequestException)
        {
            return SmsSendResult.Failed("ارتباط با قاصدک برقرار نشد.");
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
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{Base}/user/credit");
            request.Headers.Authorization = new AuthenticationHeaderValue("apikey", apiKey);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var payload = JsonSerializer.Deserialize<GhasedakEnvelope>(body, JsonOptions);
            if (!string.Equals(payload?.Result, "success", StringComparison.OrdinalIgnoreCase))
            {
                return new SmsCreditResult(false, null, $"قاصدک: {payload?.Message ?? "خطای نامشخص"}");
            }

            var creditText = payload?.Items?.FirstOrDefault()?.Credit;            return decimal.TryParse(creditText, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var credit)
                ? new SmsCreditResult(true, credit, null)
                : new SmsCreditResult(false, null, "اعتبار قابل خواندن نبود.");
        }
        catch (TaskCanceledException)
        {
            return new SmsCreditResult(false, null, SmsSendErrors.AmbiguousTimeout);
        }
        catch (HttpRequestException)
        {
            return new SmsCreditResult(false, null, "ارتباط با قاصدک برقرار نشد.");
        }
    }

    public Task<SmsMessageStatusResult> GetMessageStatusAsync(
        string providerMessageId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new SmsMessageStatusResult(
            false,
            SmsOutcomeStatus.Unknown,
            null,
            "وضعیت قاصدک فقط با Webhook گزارش تحویل به‌روزرسانی می‌شود."));

    public async Task<SmsConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var credit = await GetCreditAsync(cancellationToken);
        return credit.Success
            ? new SmsConnectionTestResult(true, "اتصال به قاصدک برقرار است.", credit.Credit)
            : new SmsConnectionTestResult(false, credit.Error ?? "اتصال برقرار نشد.", null);
    }

    private sealed class GhasedakEnvelope
    {
        public string? Result { get; set; }
        public string? Message { get; set; }
        public List<GhasedakItem>? Items { get; set; }
    }

    private sealed class GhasedakItem
    {
        public long? MessageId { get; set; }
        public string? Credit { get; set; }
    }
}
