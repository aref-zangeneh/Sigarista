using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sugarista.Application.Sms;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Sms;

/// <summary>
/// Melipayamak (payamak-panel.com) adapter: POST form bodies with
/// username/password auth. The classic SendSMS API has no per-recipient
/// bulk contract, so the runner falls back to single sends.
/// </summary>
public sealed class MelipayamakGateway(
    HttpClient httpClient,
    ILogger<MelipayamakGateway> logger,
    string? username,
    string? password,
    string? baseUrl) : ISmsProviderGateway
{
    private const string DefaultBase = "https://rest.payamak-panel.com/api/SendSMS";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private string Base => string.IsNullOrWhiteSpace(baseUrl) ? DefaultBase : baseUrl.TrimEnd('/');

    public SmsProviderType ProviderType => SmsProviderType.Melipayamak;

    public bool SupportsSingleSend => true;
    public bool SupportsBulkSend => false;
    public bool SupportsPatternSend => false;
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
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return SmsSendResult.Failed("نام کاربری/رمز عبور ملی پیامک تنظیم نشده است.");
        }

        if (string.IsNullOrWhiteSpace(senderLine))
        {
            return SmsSendResult.Failed("خط ارسال برای ملی پیامک الزامی است.");
        }

        try
        {
            var response = await httpClient.PostAsync(
                $"{Base}/SendSMS",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["username"] = username,
                    ["password"] = password,
                    ["to"] = mobile,
                    ["from"] = senderLine,
                    ["text"] = message,
                    ["isflash"] = "false",
                }),
                cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Melipayamak send failed ({Code}): {Body}", (int)response.StatusCode, body);
                return SmsSendResult.Failed($"ملی پیامک درخواست را نپذیرفت (کد {(int)response.StatusCode}).");
            }

            var payload = JsonSerializer.Deserialize<MelipayamakEnvelope>(body, JsonOptions);
            if (payload?.RetStatus is not 1)
            {
                return SmsSendResult.Failed($"ملی پیامک: {payload?.StrRetStatus ?? payload?.Message ?? "خطای نامشخص"}");
            }

            return SmsSendResult.Ok(payload.Value?.ToString(), payload.StrRetStatus, null);
        }
        catch (TaskCanceledException)
        {
            return SmsSendResult.Failed(SmsSendErrors.AmbiguousTimeout);
        }
        catch (HttpRequestException)
        {
            return SmsSendResult.Failed("ارتباط با ملی پیامک برقرار نشد.");
        }
    }

    public Task<SmsBulkResult> SendBulkAsync(
        IReadOnlyList<string> mobiles,
        string message,
        string? senderLine,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(SmsBulkResult.Failed("این Provider ارسال گروهی بومی ندارد."));

    public Task<SmsSendResult> SendPatternAsync(
        string mobile,
        string providerTemplateId,
        IReadOnlyDictionary<string, string> values,
        string? senderLine,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(SmsSendResult.Failed("این Provider ارسال قالبی در این آداپتور پشتیبانی نمی‌شود."));

    public async Task<SmsCreditResult> GetCreditAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return new SmsCreditResult(false, null, "نام کاربری/رمز عبور تنظیم نشده است.");
        }

        try
        {
            var response = await httpClient.PostAsync(
                $"{Base}/GetCredit",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["username"] = username,
                    ["password"] = password,
                }),
                cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var payload = JsonSerializer.Deserialize<MelipayamakEnvelope>(body, JsonOptions);
            if (payload?.RetStatus is not 1)
            {
                return new SmsCreditResult(false, null, $"ملی پیامک: {payload?.StrRetStatus ?? "خطای نامشخص"}");
            }

            return decimal.TryParse(payload.Value?.ToString(), out var credit)
                ? new SmsCreditResult(true, credit, null)
                : new SmsCreditResult(false, null, "اعتبار قابل خواندن نبود.");
        }
        catch (TaskCanceledException)
        {
            return new SmsCreditResult(false, null, SmsSendErrors.AmbiguousTimeout);
        }
        catch (HttpRequestException)
        {
            return new SmsCreditResult(false, null, "ارتباط با ملی پیامک برقرار نشد.");
        }
    }

    public Task<SmsMessageStatusResult> GetMessageStatusAsync(
        string providerMessageId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new SmsMessageStatusResult(
            false,
            SmsOutcomeStatus.Unknown,
            null,
            "وضعیت ملی پیامک فقط با Webhook گزارش تحویل به‌روزرسانی می‌شود."));

    public async Task<SmsConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var credit = await GetCreditAsync(cancellationToken);
        return credit.Success
            ? new SmsConnectionTestResult(true, "اتصال به ملی پیامک برقرار است.", credit.Credit)
            : new SmsConnectionTestResult(false, credit.Error ?? "اتصال برقرار نشد.", null);
    }

    private sealed class MelipayamakEnvelope
    {
        public int? RetStatus { get; set; }
        public string? StrRetStatus { get; set; }
        public string? Message { get; set; }
        public object? Value { get; set; }
    }
}
