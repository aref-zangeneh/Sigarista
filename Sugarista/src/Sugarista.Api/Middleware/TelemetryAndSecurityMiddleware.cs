using System.Diagnostics;
using Sugarista.Api.Monitoring;

namespace Sugarista.Api.Middleware;

/// <summary>
/// Ultra-low-overhead HTTP telemetry &amp; security observer.
/// Records request duration, status codes, unhandled exceptions, and suspicious
/// security patterns into bounded in-memory buffers with ZERO database writes.
/// </summary>
public sealed class TelemetryAndSecurityMiddleware
{
    private readonly RequestDelegate _next;
    private readonly SystemTelemetryCollector _collector;

    public TelemetryAndSecurityMiddleware(RequestDelegate next, SystemTelemetryCollector collector)
    {
        _next = next;
        _collector = collector;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "/";

        // Never self-observe long-lived SSE monitoring/MOQ streams to avoid skewing latency stats.
        if (path.EndsWith("/stream", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var startTs = Stopwatch.GetTimestamp();
        _collector.BeginRequest();
        string? exceptionMessage = null;

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            exceptionMessage = $"{ex.GetType().Name}: {ex.Message}";
            throw;
        }
        finally
        {
            _collector.EndRequest();
            var elapsedMs = Stopwatch.GetElapsedTime(startTs).TotalMilliseconds;
            var statusCode = exceptionMessage is not null && context.Response.StatusCode < 500
                ? StatusCodes.Status500InternalServerError
                : context.Response.StatusCode;

            var clientIp = ResolveClientIp(context);
            _collector.RecordRequest(
                context.Request.Method,
                path,
                context.Request.QueryString.Value,
                statusCode,
                elapsedMs,
                clientIp,
                exceptionMessage);
        }
    }

    private static string ResolveClientIp(HttpContext context)
    {
        var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            var first = forwarded.Split(',')[0].Trim();
            if (!string.IsNullOrWhiteSpace(first)) return first;
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
