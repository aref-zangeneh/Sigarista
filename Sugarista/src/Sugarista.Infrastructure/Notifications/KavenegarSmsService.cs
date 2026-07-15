using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Common.Options;

namespace Sugarista.Infrastructure.Notifications;

public class KavenegarSmsService : ISmsService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<KavenegarSmsService> _logger;
    private readonly SmsOptions _options;

    public KavenegarSmsService(
        HttpClient httpClient,
        ILogger<KavenegarSmsService> logger,
        IOptions<SmsOptions> options)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;
    }

    public async Task SendOrderReadyAsync(
        string phoneNumber,
        string trackingCode,
        string customerName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("Kavenegar API key is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_options.Sender))
        {
            throw new InvalidOperationException("Kavenegar sender number is not configured.");
        }

        var message = LoggingSmsService.BuildMessage(trackingCode, customerName, _options.CafeName);
        var normalizedPhone = NormalizePhone(phoneNumber);
        var url =
            $"https://api.kavenegar.com/v1/{_options.ApiKey}/sms/send.json?receptor={Uri.EscapeDataString(normalizedPhone)}&sender={Uri.EscapeDataString(_options.Sender)}&message={Uri.EscapeDataString(message)}";

        using var response = await _httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Kavenegar SMS failed ({StatusCode}): {Body}", (int)response.StatusCode, body);
            throw new InvalidOperationException("SMS provider rejected the request.");
        }

        var payload = await response.Content.ReadFromJsonAsync<KavenegarResponse>(cancellationToken);
        if (payload?.Return?.Status is not 200)
        {
            _logger.LogError("Kavenegar SMS returned error: {Status}", payload?.Return?.Status);
            throw new InvalidOperationException(payload?.Return?.Message ?? "SMS provider returned an error.");
        }
    }

    private static string NormalizePhone(string phoneNumber)
    {
        var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("98", StringComparison.Ordinal))
        {
            return digits;
        }

        if (digits.StartsWith('0'))
        {
            return "98" + digits[1..];
        }

        return digits;
    }

    private sealed class KavenegarResponse
    {
        public KavenegarReturn? Return { get; set; }
    }

    private sealed class KavenegarReturn
    {
        public int Status { get; set; }

        public string? Message { get; set; }
    }
}
