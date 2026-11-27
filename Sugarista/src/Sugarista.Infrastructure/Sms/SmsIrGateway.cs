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
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = JsonContent.Create(new
            {
                lineNumber = senderLine,
                messageText = message,
                mobiles = mobiles.ToArray()
            });

            using var response = await httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var (payload, parseError) = ParseResponse(response, body, "send/bulk");
            if (parseError is not null)
            {
                return SmsBulkResult.Failed(parseError);
            }

            if (payload?.Status is not 1)
            {
                return SmsBulkResult.Failed($"SMS.ir: {payload?.Message ?? "خطای نامشخص"}");
            }

            var rawCost = ExtractDecimal(payload.Data, "cost");
            var cost = rawCost is > 0 ? rawCost / 10m : (decimal?)null; // Rial → Toman
            var messageId = ExtractMessageId(payload.Data);
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
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            object templateIdValue = int.TryParse(providerTemplateId, out var numericTemplateId)
                ? numericTemplateId
                : providerTemplateId;
            request.Content = JsonContent.Create(new
            {
                mobile = mobile,
                templateId = templateIdValue,
                parameters = values.Select(pair => new { name = pair.Key, value = pair.Value }).ToArray()
            });

            using var response = await httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var (payload, parseError) = ParseResponse(response, body, "send/verify");
            if (parseError is not null)
            {
                return SmsSendResult.Failed(parseError);
            }

            if (payload?.Status is not 1)
            {
                return SmsSendResult.Failed($"SMS.ir: {payload?.Message ?? "خطای نامشخص"}");
            }

            var rawCost = ExtractDecimal(payload.Data, "cost");
            var cost = rawCost is > 0 ? rawCost / 10m : (decimal?)null; // Rial → Toman
            return SmsSendResult.Ok(ExtractMessageId(payload.Data), payload.Message, cost);
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
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var response = await httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var (payload, parseError) = ParseResponse(response, body, "credit");
            if (parseError is not null)
            {
                return new SmsCreditResult(false, null, parseError);
            }

            if (payload?.Status is not 1)
            {
                return new SmsCreditResult(false, null, $"SMS.ir: {payload?.Message ?? "خطای نامشخص"}");
            }

            var credit = ExtractCredit(payload.Data);
            return new SmsCreditResult(true, credit, null);
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

    public async Task<SmsMessageStatusResult> GetMessageStatusAsync(
        string providerMessageId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new SmsMessageStatusResult(false, SmsOutcomeStatus.Unknown, null, "کلید API سرویس SMS.ir تنظیم نشده است.");
        }

        if (string.IsNullOrWhiteSpace(providerMessageId))
        {
            return new SmsMessageStatusResult(false, SmsOutcomeStatus.Unknown, null, "شناسه پیامک برای استعلام موجود نیست.");
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{Base}/send/{Uri.EscapeDataString(providerMessageId.Trim())}");
            request.Headers.Add("x-api-key", apiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var (payload, parseError) = ParseResponse(response, body, "send/report");
            if (parseError is not null)
            {
                return new SmsMessageStatusResult(false, SmsOutcomeStatus.Unknown, null, parseError);
            }

            if (payload?.Status is not 1)
            {
                return new SmsMessageStatusResult(false, SmsOutcomeStatus.Unknown, null, $"SMS.ir: {payload?.Message ?? "خطای نامشخص"}");
            }

            var deliveryState = (int?)ExtractDecimal(payload.Data, "deliveryState");
            var rawCost = ExtractDecimal(payload.Data, "cost");
            var cost = rawCost is > 0 ? rawCost / 10m : (decimal?)null;

            // SMS.ir v1 deliveryState codes:
            // 1 = رسیده به گوشی (Delivered)
            // 2 = نرسیده به گوشی (Not delivered / Failed)
            // 3 = پردازش در مخابرات (Accepted)
            // 4 = نرسیده به مخابرات / در صف (Accepted)
            // 5 = رسیده به اپراتور (Sent)
            // 6 = ناموفق (Failed)
            // 7 = لیست سیاه مخابرات (Rejected)
            var mapped = MapSmsIrDeliveryState(deliveryState);
            var note = deliveryState switch
            {
                1 => "رسیده به گوشی گیرنده",
                2 => "نرسیده به گوشی گیرنده",
                3 => "در حال پردازش در مخابرات",
                4 => "در صف ارسال اپراتور",
                5 => "رسیده به مخابرات (در انتظار تحویل به گوشی)",
                6 => "خطا در ارسال پیامک",
                7 => "مسدودشده توسط لیست سیاه مخابرات",
                _ => payload.Message
            };

            return new SmsMessageStatusResult(true, mapped, cost, note);
        }
        catch (TaskCanceledException)
        {
            return new SmsMessageStatusResult(false, SmsOutcomeStatus.Unknown, null, SmsSendErrors.AmbiguousTimeout);
        }
        catch (HttpRequestException)
        {
            return new SmsMessageStatusResult(false, SmsOutcomeStatus.Unknown, null, "ارتباط با SMS.ir برقرار نشد.");
        }
    }

    public static SmsOutcomeStatus MapSmsIrDeliveryState(int? deliveryState) =>
        deliveryState switch
        {
            1 => SmsOutcomeStatus.Delivered,
            2 or 6 => SmsOutcomeStatus.Failed,
            3 or 4 => SmsOutcomeStatus.Accepted,
            5 => SmsOutcomeStatus.Sent,
            7 => SmsOutcomeStatus.Rejected,
            _ => SmsOutcomeStatus.Unknown
        };

    public async Task<SmsConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var credit = await GetCreditAsync(cancellationToken);
        return credit.Success
            ? new SmsConnectionTestResult(true, "اتصال به SMS.ir برقرار است.", credit.Credit)
            : new SmsConnectionTestResult(false, credit.Error ?? "اتصال برقرار نشد.", null);
    }

    private (SmsIrEnvelope? Payload, string? Error) ParseResponse(
        HttpResponseMessage response,
        string body,
        string operation)
    {
        var statusCode = (int)response.StatusCode;
        SmsIrEnvelope? payload = null;

        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                payload = JsonSerializer.Deserialize<SmsIrEnvelope>(body, JsonOptions);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(
                    ex,
                    "SMS.ir {Operation} returned non-JSON body (HTTP {Code}): {Body}",
                    operation,
                    statusCode,
                    body.Length > 300 ? body[..300] : body);
            }
        }

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "SMS.ir {Operation} failed (HTTP {Code}): {Body}",
                operation,
                statusCode,
                string.IsNullOrWhiteSpace(body) ? "<empty>" : (body.Length > 300 ? body[..300] : body));

            if (!string.IsNullOrWhiteSpace(payload?.Message))
            {
                return (null, $"SMS.ir: {payload.Message} (کد {statusCode})");
            }

            if (statusCode is 401 or 403)
            {
                return (null, $"SMS.ir کلید API یا دسترسی IP سرور را نپذیرفت (کد {statusCode}).");
            }

            if (statusCode == 404)
            {
                return (null, $"آدرس سرویس SMS.ir (Base URL) نامعتبر است (کد ۴۰۴). مقدار پیش‌فرض: {DefaultBase}");
            }

            return (null, $"SMS.ir درخواست را نپذیرفت (کد {statusCode}).");
        }

        if (payload is null)
        {
            logger.LogWarning("SMS.ir {Operation} returned empty or invalid JSON (HTTP {Code}).", operation, statusCode);
            return (null, $"پاسخ خالی یا نامعتبر از SMS.ir دریافت شد (کد {statusCode}).");
        }

        return (payload, null);
    }

    private static decimal? ExtractCredit(JsonElement? data)
    {
        if (data is not { } element)
        {
            return null;
        }

        // Official SMS.ir REST v1 `/v1/credit` returns `"data": 1234.56` directly.
        if (element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out var direct))
        {
            return direct;
        }

        if (element.ValueKind == JsonValueKind.String
            && decimal.TryParse(
                element.GetString(),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var parsed))
        {
            return parsed;
        }

        // Also support object shape `"data": { "credit": 1234.56 }` for backwards compatibility.
        return ExtractDecimal(data, "credit");
    }

    private static decimal? ExtractDecimal(JsonElement? data, string propertyName)
    {
        if (data is not { ValueKind: JsonValueKind.Object } obj)
        {
            return null;
        }

        foreach (var prop in obj.EnumerateObject())
        {
            if (!string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetDecimal(out var number))
            {
                return number;
            }

            if (prop.Value.ValueKind == JsonValueKind.String
                && decimal.TryParse(
                    prop.Value.GetString(),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static string? ExtractMessageId(JsonElement? data)
    {
        if (data is not { ValueKind: JsonValueKind.Object } obj)
        {
            return null;
        }

        foreach (var prop in obj.EnumerateObject())
        {
            if (string.Equals(prop.Name, "messageId", StringComparison.OrdinalIgnoreCase)
                || string.Equals(prop.Name, "packId", StringComparison.OrdinalIgnoreCase))
            {
                return prop.Value.ValueKind switch
                {
                    JsonValueKind.Number => prop.Value.GetRawText(),
                    JsonValueKind.String => prop.Value.GetString(),
                    _ => null,
                };
            }

            if (string.Equals(prop.Name, "messageIds", StringComparison.OrdinalIgnoreCase)
                && prop.Value.ValueKind == JsonValueKind.Array)
            {
                var first = prop.Value.EnumerateArray().FirstOrDefault();
                if (first.ValueKind is JsonValueKind.Number or JsonValueKind.String)
                {
                    return first.ValueKind == JsonValueKind.String ? first.GetString() : first.GetRawText();
                }
            }
        }

        return null;
    }

    private sealed class SmsIrEnvelope
    {
        public int? Status { get; set; }
        public string? Message { get; set; }
        public JsonElement? Data { get; set; }
    }
}
