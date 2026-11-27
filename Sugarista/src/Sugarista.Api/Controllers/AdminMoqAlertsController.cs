using System.Text.Json;
using System.Text.Json.Serialization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Api.Authorization;
using Sugarista.Application.MoqAlerts;
using Sugarista.Application.MoqAlerts.Commands.QuickRestockMoqItem;
using Sugarista.Application.MoqAlerts.Commands.UpdateMoqThreshold;
using Sugarista.Application.MoqAlerts.Queries.GetMoqAlerts;
using Sugarista.Domain.Constants;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Api.Controllers;

/// <summary>
/// Real-time MOQ (Minimum Order Quantity) & low-stock alert center.
/// Provides both an instant Server-Sent Events (SSE) push stream and
/// fast REST endpoints for viewing, filtering, configuring MOQ, and quick-restocking.
/// </summary>
[Authorize]
[Route("api/admin/moq-alerts")]
public class AdminMoqAlertsController : ApiControllerBase
{
    private static readonly JsonSerializerOptions SseJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(25);
    private static readonly TimeSpan DebounceWindow = TimeSpan.FromMilliseconds(220);

    private readonly IMoqAlertBroadcaster _broadcaster;
    private readonly IServiceScopeFactory _scopeFactory;

    public AdminMoqAlertsController(
        IMoqAlertBroadcaster broadcaster,
        IServiceScopeFactory scopeFactory)
    {
        _broadcaster = broadcaster;
        _scopeFactory = scopeFactory;
    }

    [HttpGet]
    [RequirePermission(AdminPermissions.MoqAlerts.View)]
    public async Task<ActionResult<MoqAlertSnapshotDto>> Get(
        [FromQuery] MoqSourceType? sourceType = null,
        [FromQuery] MoqSeverity? severity = null,
        [FromQuery] string? search = null,
        [FromQuery] bool includeHealthy = false,
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(
            new GetMoqAlertsQuery(sourceType, severity, search, includeHealthy),
            cancellationToken));
    }

    /// <summary>
    /// Real-time Server-Sent Events stream pushing MOQ snapshots the instant
    /// any stock or threshold changes — zero polling, zero DB connection pinning.
    /// </summary>
    [HttpGet("stream")]
    [RequirePermission(AdminPermissions.MoqAlerts.View)]
    public async Task Stream(CancellationToken cancellationToken)
    {
        Response.StatusCode = StatusCodes.Status200OK;
        Response.Headers.ContentType = "text/event-stream; charset=utf-8";
        Response.Headers.CacheControl = "no-cache, no-transform";
        Response.Headers["X-Accel-Buffering"] = "no";
        Response.Headers.Connection = "keep-alive";

        var (reader, subscription) = _broadcaster.Subscribe();
        using var subscriptionScope = subscription;

        // 1. Push initial snapshot immediately upon connect.
        await WriteSnapshotEventAsync(cancellationToken);

        // 2. Wait for stock change signals or send lightweight keep-alive pings.
        while (!cancellationToken.IsCancellationRequested)
        {
            using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            heartbeatCts.CancelAfter(HeartbeatInterval);

            bool hasUpdate;
            try
            {
                hasUpdate = await reader.WaitToReadAsync(heartbeatCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Heartbeat timeout — send SSE comment ping so reverse proxies keep the stream alive.
                await Response.WriteAsync(": ping\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
                continue;
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (!hasUpdate)
            {
                break;
            }

            // Drain bursts (e.g. a multi-line POS order deducting 10 ingredients at once)
            // into a single crisp push after a short 220ms coalesce window.
            while (reader.TryRead(out _)) { }
            await Task.Delay(DebounceWindow, cancellationToken);
            while (reader.TryRead(out _)) { }

            await WriteSnapshotEventAsync(cancellationToken);
        }
    }

    [HttpPut("threshold")]
    [RequirePermission(AdminPermissions.MoqAlerts.Update)]
    public async Task<ActionResult<MoqAlertItemDto>> UpdateThreshold(
        [FromBody] UpdateMoqThresholdCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await Mediator.Send(command, cancellationToken));
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(exception.Message);
        }
        catch (DomainException exception)
        {
            return Problem(exception.Message);
        }
    }

    [HttpPost("quick-restock")]
    [RequirePermission(AdminPermissions.MoqAlerts.Update)]
    public async Task<ActionResult<MoqAlertItemDto>> QuickRestock(
        [FromBody] QuickRestockMoqItemCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await Mediator.Send(command, cancellationToken));
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(exception.Message);
        }
        catch (DomainException exception)
        {
            return Problem(exception.Message);
        }
    }

    private async Task WriteSnapshotEventAsync(CancellationToken cancellationToken)
    {
        // Use a short-lived DI scope so DbContext is released immediately after each push.
        using var scope = _scopeFactory.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var snapshot = await sender.Send(new GetMoqAlertsQuery(), cancellationToken);

        var json = JsonSerializer.Serialize(snapshot, SseJsonOptions);
        await Response.WriteAsync($"event: moq_snapshot\nid: {snapshot.Version}\ndata: {json}\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }
}
