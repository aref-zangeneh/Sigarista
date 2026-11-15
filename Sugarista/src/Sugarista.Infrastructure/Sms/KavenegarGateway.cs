using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sugarista.Application.Sms;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Sms;

/// <summary>
/// Kavenegar adapter. REST v1, API key in the URL path. Maps Kavenegar's
/// message status codes to the standard lifecycle (4→Sent, 5/8→Delivered,
/// 6/13→Failed, 11/14→Rejected, else Unknown).
/// </summary>
public sealed class KavenegarGateway(
    HttpClient httpClient,
    ILogger<KavenegarGateway> logger,
    string? apiKey,
    string? baseUrl) : ISmsProviderGateway
{
    private const string DefaultBase = "https://api.kavenegar.com/v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private string Base => string.IsNullOrWhiteSpace(baseUrl) ? DefaultBase : baseUrl.TrimEnd('/');

    public SmsProviderType ProviderType => SmsProviderType.Kavenegar;

    public bool SupportsSingleSend => true;
    public bool SupportsBulkSend => false; // no clean per-recipient bulk contract → runner uses single sends
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
            return SmsSendResult.Failed("کلید API کاوه‌نگار تنظیم نشده است.");
        }

        var url = $"{Base}/{Uri.EscapeDataString(apiKey)}/sms/send.json" +
                  $"?receptor={Uri.EscapeDataString(mobile)}" +
                  (string.IsNullOrWhiteSpace(senderLine) ? "" : $"&sender={Uri.EscapeDataString(senderLine)}") +
                  $"&message={Uri.EscapeDataString(message)}";

        try
        {
            using var response = await httpClient.GetAsync(url, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Kavenegar send failed ({Code}): {Body}", (int)response.StatusCode, body);
                return SmsSendResult.Failed($"کاوه‌نگار درخواست را نپذیرفت (کد {(int)response.StatusCode}).");
            }

            var payload = JsonSerializer.Deserialize<KavenegarEnvelope>(body, JsonOptions);
            if (payload?.Return?.Status is not 200)
            {
                return SmsSendResult.Failed($"کاوه‌نگار: {payload?.Return?.Message ?? "خطای نامشخص"}");
            }

            var entry = payload.Entries?.FirstOrDefault();
            return SmsSendResult.Ok(
                entry?.MessageId?.ToString(),
                entry?.StatusText,
                entry?.Cost is > 0 ? entry.Cost / 10m : null); // API reports Rial
        }
        catch (TaskCanceledException)
        {
            return SmsSendResult.Failed(SmsSendErrors.AmbiguousTimeout);
        }
        catch (HttpRequestException)
        {
            return SmsSendResult.Failed("ارتباط با کاوه‌نگار برقرار نشد.");
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
            return SmsSendResult.Failed("کلید API کاوه‌نگار تنظیم نشده است.");
        }

        // Kavenegar lookup accepts token/token2/token3.
        var tokens = values.Values.Take(3).ToList();
        var url = $"{Base}/{Uri.EscapeDataString(apiKey)}/verify/lookup.json" +
                  $"?receptor={Uri.EscapeDataString(mobile)}" +
                  $"&template={Uri.EscapeDataString(providerTemplateId)}" +
                  string.Join("", tokens.Select((value, index) =>
                      $"&token{(index == 0 ? "" : index + 1)}={Uri.EscapeDataString(value)}"));

        try
        {
            using var response = await httpClient.GetAsync(url, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var payload = JsonSerializer.Deserialize<KavenegarEnvelope>(body, JsonOptions);
            if (payload?.Return?.Status is not 200)
            {
                return SmsSendResult.Failed($"کاوه‌نگار Lookup: {payload?.Return?.Message ?? "خطای نامشخص"}");
            }

            return SmsSendResult.Ok(
                payload.Entries?.FirstOrDefault()?.MessageId?.ToString(),
                null,
                null);
        }
        catch (TaskCanceledException)
        {
            return SmsSendResult.Failed(SmsSendErrors.AmbiguousTimeout);
        }
        catch (HttpRequestException)
        {
            return SmsSendResult.Failed("ارتباط با کاوه‌نگار برقرار نشد.");
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
            using var response = await httpClient.GetAsync(
                $"{Base}/{Uri.EscapeDataString(apiKey)}/account/info.json",
                cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var payload = JsonSerializer.Deserialize<KavenegarEnvelope>(body, JsonOptions);
            var creditText = payload?.Entries?.FirstOrDefault()?.Credit;
            return decimal.TryParse(creditText, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var credit)
                ? new SmsCreditResult(true, credit, null)
                : new SmsCreditResult(false, null, "اعتبار قابل خواندن نبود.");
        }
        catch (TaskCanceledException)
        {
            return new SmsCreditResult(false, null, SmsSendErrors.AmbiguousTimeout);
        }
        catch (HttpRequestException)
        {
            return new SmsCreditResult(false, null, "ارتباط با کاوه‌نگار برقرار نشد.");
        }
    }

    public async Task<SmsMessageStatusResult> GetMessageStatusAsync(
        string providerMessageId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new SmsMessageStatusResult(false, SmsOutcomeStatus.Unknown, null, "کلید API تنظیم نشده است.");
        }

        try
        {
            using var response = await httpClient.GetAsync(
                $"{Base}/{Uri.EscapeDataString(apiKey)}/sms/status.json?messageid={Uri.EscapeDataString(providerMessageId)}",
                cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var payload = JsonSerializer.Deserialize<KavenegarStatusEnvelope>(body, JsonOptions);
            var entry = payload?.Entries?.FirstOrDefault();
            if (payload?.Return?.Status is not 200 || entry is null)
            {
                return new SmsMessageStatusResult(false, SmsOutcomeStatus.Unknown, null, "وضعیت قابل خواندن نبود.");
            }

            return new SmsMessageStatusResult(true, MapStatus(entry.Status), null, null);
        }
        catch (TaskCanceledException)
        {
            return new SmsMessageStatusResult(false, SmsOutcomeStatus.Unknown, null, SmsSendErrors.AmbiguousTimeout);
        }
        catch (HttpRequestException)
        {
            return new SmsMessageStatusResult(false, SmsOutcomeStatus.Unknown, null, "ارتباط با کاوه‌نگار برقرار نشد.");
        }
    }

    public async Task<SmsConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var credit = await GetCreditAsync(cancellationToken);
        return credit.Success
            ? new SmsConnectionTestResult(true, "اتصال به کاوه‌نگار برقرار است.", credit.Credit)
            : new SmsConnectionTestResult(false, credit.Error ?? "اتصال برقرار نشد.", null);
    }

    public static SmsOutcomeStatus MapStatus(int status) => status switch
    {
        4 => SmsOutcomeStatus.Sent,
        5 or 8 => SmsOutcomeStatus.Delivered,
        6 or 13 => SmsOutcomeStatus.Failed,
        11 or 14 => SmsOutcomeStatus.Rejected,
        _ => SmsOutcomeStatus.Unknown
    };

    private sealed class KavenegarEnvelope
    {
        public KavenegarReturn? Return { get; set; }
        public List<KavenegarEntry>? Entries { get; set; }
    }

    private sealed class KavenegarReturn
    {
        public int Status { get; set; }
        public string? Message { get; set; }
    }

    private sealed class KavenegarEntry
    {
        public long? MessageId { get; set; }
        public int? Status { get; set; }
        public string? StatusText { get; set; }
        public string? Credit { get; set; }
        public long? Cost { get; set; }
    }

    private sealed class KavenegarStatusEnvelope
    {
        public KavenegarReturn? Return { get; set; }
        public List<KavenegarStatusEntry>? Entries { get; set; }
    }

    private sealed class KavenegarStatusEntry
    {
        public int Status { get; set; }
        public string? Statustext { get; set; }
    }
}
