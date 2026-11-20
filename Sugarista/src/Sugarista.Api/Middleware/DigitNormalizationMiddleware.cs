using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Sugarista.Application.Common.Text;

namespace Sugarista.Api.Middleware;

/// <summary>
/// Normalizes Persian and Arabic-Indic digits on incoming query parameters and JSON request bodies
/// to canonical ASCII digits before model binding, while leaving binary uploads and sensitive fields untouched.
/// </summary>
public sealed class DigitNormalizationMiddleware
{
    private const long MaxJsonNormalizationBytes = 4 * 1024 * 1024;
    private readonly RequestDelegate _next;

    public DigitNormalizationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        if (path.StartsWithSegments("/health") || path.StartsWithSegments("/api/files"))
        {
            await _next(context);
            return;
        }

        NormalizeQueryString(context.Request);

        if (ShouldNormalizeJsonBody(context.Request))
        {
            await NormalizeJsonBodyAsync(context);
        }

        await _next(context);
    }

    private static void NormalizeQueryString(HttpRequest request)
    {
        if (!request.QueryString.HasValue)
        {
            return;
        }

        var raw = request.QueryString.Value!;
        // Percent-encoded UTF-8 prefixes for ۰-۹ (%DB%B0-%DB%B9) and ٠-٩ (%D9%A0-%D9%A9).
        var mayHaveEncodedDigits =
            raw.Contains("%DB%B", StringComparison.OrdinalIgnoreCase) ||
            raw.Contains("%D9%A", StringComparison.OrdinalIgnoreCase);

        if (!mayHaveEncodedDigits && !DigitNormalizer.ContainsNonAsciiDigits(raw))
        {
            return;
        }

        var parsed = QueryHelpers.ParseQuery(raw);
        var pairs = new List<KeyValuePair<string, string?>>(parsed.Count);
        var changed = false;

        foreach (var (key, values) in parsed)
        {
            var isSensitive = DigitNormalizer.IsSensitiveFieldName(key);
            foreach (var value in values)
            {
                if (isSensitive || value is null)
                {
                    pairs.Add(new KeyValuePair<string, string?>(key, value));
                    continue;
                }

                var converted = DigitNormalizer.ToEnglishDigits(value);
                if (!ReferenceEquals(converted, value) && !string.Equals(converted, value, StringComparison.Ordinal))
                {
                    changed = true;
                }

                pairs.Add(new KeyValuePair<string, string?>(key, converted));
            }
        }

        if (changed)
        {
            request.QueryString = QueryString.Create(pairs);
        }
    }

    private static bool ShouldNormalizeJsonBody(HttpRequest request)
    {
        var method = request.Method;
        if (!HttpMethods.IsPost(method) &&
            !HttpMethods.IsPut(method) &&
            !HttpMethods.IsPatch(method) &&
            !HttpMethods.IsDelete(method))
        {
            return false;
        }

        if (!request.HasJsonContentType())
        {
            return false;
        }

        return request.ContentLength is null or <= MaxJsonNormalizationBytes;
    }

    private static async Task NormalizeJsonBodyAsync(HttpContext context)
    {
        var request = context.Request;
        request.EnableBuffering();

        string bodyText;
        using (var reader = new StreamReader(
            request.Body,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 4096,
            leaveOpen: true))
        {
            bodyText = await reader.ReadToEndAsync(context.RequestAborted);
        }

        if (JsonDigitNormalizer.TryNormalizeJson(bodyText, out var normalizedJson))
        {
            var bytes = Encoding.UTF8.GetBytes(normalizedJson);
            var replacement = new MemoryStream(bytes);
            context.Response.RegisterForDisposeAsync(replacement);
            request.Body = replacement;
            request.ContentLength = bytes.Length;
            return;
        }

        if (request.Body.CanSeek)
        {
            request.Body.Position = 0;
        }
    }
}
