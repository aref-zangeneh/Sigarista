using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Application.Sms;

namespace Sugarista.Api.Controllers;

/// <summary>
/// Public delivery-report endpoint the provider panels POST to
/// (URL: api/sms/webhook/{providerId}/{secret}). Providers use wildly
/// different payload formats — form-encoded (Kavenegar, Ghasedak, IPPanel),
/// JSON (SMS.ir) or query params — so the raw request is normalized here and
/// the shared handler does status mapping + idempotent DB updates.
/// Always answers 200 so providers do not retry-spam on parse hiccups.
/// </summary>
[AllowAnonymous]
[ApiController]
[Route("api/sms/webhook")]
public class SmsDeliveryWebhookController : ControllerBase
{
    private readonly IMediator mediator;

    public SmsDeliveryWebhookController(IMediator mediator)
    {
        this.mediator = mediator;
    }

    [HttpPost("{providerId:guid}/{secret}")]
    public async Task<IActionResult> Receive(Guid providerId, string secret, CancellationToken cancellationToken)
    {
        var (messageId, statusText, statusCode, receptor) = await ExtractPayloadAsync(cancellationToken);

        var ack = await mediator.Send(
            new RecordSmsDeliveryReportCommand(
                new SmsDeliveryReportData(providerId, secret, messageId, statusText, statusCode, receptor)),
            cancellationToken);

        // Shape varies by provider, but every one of them treats 200 + "ok" as success.
        return Ok(new { ok = true, handled = ack.Handled, message = ack.Message });
    }

    [HttpGet("{providerId:guid}/{secret}")]
    public async Task<IActionResult> ReceiveGet(Guid providerId, string secret, CancellationToken cancellationToken)
    {
        var (messageId, statusText, statusCode, receptor) = ExtractFromQuery();

        var ack = await mediator.Send(
            new RecordSmsDeliveryReportCommand(
                new SmsDeliveryReportData(providerId, secret, messageId, statusText, statusCode, receptor)),
            cancellationToken);
        return Ok(new { ok = true, handled = ack.Handled, message = ack.Message });
    }

    private async Task<(string? MessageId, string? StatusText, int? StatusCode, string? Receptor)>
        ExtractPayloadAsync(CancellationToken cancellationToken)
    {
        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync(cancellationToken);
            return (
                FirstValue(form, "messageid", "message_id", "messageId", "msgId"),
                FirstValue(form, "statustext", "statusText", "messageStatus"),
                FirstInt(form, "status", "statusCode"),
                FirstValue(form, "receptor", "receptor_mobile", "to", "mobile"));
        }

        if (Request.ContentLength is > 0
            && Request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true)
        {
            try
            {
                using var document = await System.Text.Json.JsonDocument.ParseAsync(Request.Body, cancellationToken: cancellationToken);
                var root = document.RootElement;
                return (
                    GetString(root, "messageid", "message_id", "messageId", "msgId", "id"),
                    GetString(root, "statustext", "statusText", "messageStatus", "deliveryStatus"),
                    GetInt(root, "status", "statusCode", "deliveryStatus"),
                    GetString(root, "receptor", "to", "mobile", "recipient"));
            }
            catch (System.Text.Json.JsonException)
            {
                return (null, null, null, null);
            }
        }

        return ExtractFromQuery();
    }

    private (string? MessageId, string? StatusText, int? StatusCode, string? Receptor) ExtractFromQuery()
    {
        string? Get(params string[] keys) => keys.Select(key => Request.Query[key].FirstOrDefault()).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        int? status = int.TryParse(Request.Query["status"].FirstOrDefault(), out var parsed)
            ? parsed : int.TryParse(Request.Query["statusCode"].FirstOrDefault(), out parsed) ? parsed : null;
        return (Get("messageid", "message_id", "messageId", "msgId", "id"), Get("statustext", "statusText", "messageStatus"), status, Get("receptor", "to", "mobile"));
    }

    private static string? FirstValue(Microsoft.AspNetCore.Http.IFormCollection form, params string[] keys) =>
        keys.Select(key => form[key].FirstOrDefault()).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static int? FirstInt(Microsoft.AspNetCore.Http.IFormCollection form, params string[] keys) =>
        keys
            .Select(key => form[key].FirstOrDefault())
            .Select(value => int.TryParse(value, out var parsed) ? parsed : (int?)null)
            .FirstOrDefault(value => value is not null);

    private static string? GetString(System.Text.Json.JsonElement root, params string[] keys)
    {
        if (root.ValueKind != System.Text.Json.JsonValueKind.Object)
        {
            return null;
        }

        foreach (var key in keys)
        {
            if (root.TryGetProperty(key, out var element)
                && element.ValueKind == System.Text.Json.JsonValueKind.String
                && !string.IsNullOrWhiteSpace(element.GetString()))
            {
                return element.GetString();
            }
            // Some providers send numeric ids.
            if (root.TryGetProperty(key, out element)
                && element.ValueKind == System.Text.Json.JsonValueKind.Number)
            {
                return element.GetRawText();
            }
        }

        return null;
    }

    private static int? GetInt(System.Text.Json.JsonElement root, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (root.ValueKind == System.Text.Json.JsonValueKind.Object
                && root.TryGetProperty(key, out var element))
            {
                if (element.ValueKind == System.Text.Json.JsonValueKind.Number && element.TryGetInt32(out var parsed))
                {
                    return parsed;
                }
                if (element.ValueKind == System.Text.Json.JsonValueKind.String
                    && int.TryParse(element.GetString(), out var fromText))
                {
                    return fromText;
                }
            }
        }
        return null;
    }
}
