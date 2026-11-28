using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sugarista.Api.Authorization;
using Sugarista.Api.Monitoring;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Constants;

namespace Sugarista.Api.Controllers;

/// <summary>
/// Smart Real-Time Server, Application (APM) &amp; Security Monitoring Controller.
/// Uses Server-Sent Events (SSE) when the admin dashboard is open (3s cadence),
/// consuming zero bandwidth/CPU when no admin is watching.
/// </summary>
[Authorize]
[Route("api/admin/monitoring")]
public class AdminMonitoringController : ApiControllerBase
{
    private static readonly JsonSerializerOptions SseJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly TimeSpan StreamInterval = TimeSpan.FromSeconds(3);

    private readonly SystemTelemetryCollector _collector;
    private readonly IServiceScopeFactory _scopeFactory;

    public AdminMonitoringController(
        SystemTelemetryCollector collector,
        IServiceScopeFactory scopeFactory)
    {
        _collector = collector;
        _scopeFactory = scopeFactory;
    }

    [HttpGet("snapshot")]
    [RequirePermission(AdminPermissions.SiteSettings.View)]
    public async Task<ActionResult<MonitoringSnapshotDto>> GetSnapshot(CancellationToken cancellationToken)
    {
        var snapshot = await BuildSnapshotAsync(Request.IsHttps, cancellationToken);
        return Ok(snapshot);
    }

    [HttpGet("stream")]
    [RequirePermission(AdminPermissions.SiteSettings.View)]
    public async Task Stream(CancellationToken cancellationToken)
    {
        Response.StatusCode = StatusCodes.Status200OK;
        Response.Headers.ContentType = "text/event-stream; charset=utf-8";
        Response.Headers.CacheControl = "no-cache, no-transform";
        Response.Headers["X-Accel-Buffering"] = "no";
        Response.Headers.Connection = "keep-alive";

        var isHttps = Request.IsHttps;

        while (!cancellationToken.IsCancellationRequested)
        {
            var snapshot = await BuildSnapshotAsync(isHttps, cancellationToken);
            var json = JsonSerializer.Serialize(snapshot, SseJsonOptions);
            await Response.WriteAsync($"event: snapshot\ndata: {json}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);

            try
            {
                await Task.Delay(StreamInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    [HttpPost("reset")]
    [RequirePermission(AdminPermissions.SiteSettings.Update)]
    public IActionResult ResetCounters()
    {
        _collector.Reset();
        return Ok(new { reset = true });
    }

    private async Task<MonitoringSnapshotDto> BuildSnapshotAsync(bool isHttps, CancellationToken cancellationToken)
    {
        var dbStart = Stopwatch.GetTimestamp();
        DatabaseHealthDto dbHealth;
        var activeAdminCount = 0;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            activeAdminCount = await dbContext.AdminUsers
                .AsNoTracking()
                .CountAsync(u => u.IsActive, cancellationToken);
            var latencyMs = Math.Round(Stopwatch.GetElapsedTime(dbStart).TotalMilliseconds, 1);
            dbHealth = new DatabaseHealthDto(true, latencyMs, "دیتابیس PostgreSQL متصل و پاسخ‌گو است.");
        }
        catch (Exception ex)
        {
            var latencyMs = Math.Round(Stopwatch.GetElapsedTime(dbStart).TotalMilliseconds, 1);
            dbHealth = new DatabaseHealthDto(false, latencyMs, $"خطا در اتصال به دیتابیس: {ex.Message}");
        }

        var audit = new SecurityConfigAuditDto(
            HttpsActive: isHttps,
            RateLimitingEnabled: true,
            SecurityHeadersEnabled: true,
            ActiveAdminUsersCount: activeAdminCount);

        return _collector.CaptureSnapshot(dbHealth, audit);
    }
}
