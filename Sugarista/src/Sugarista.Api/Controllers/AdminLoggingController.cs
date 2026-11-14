using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog.Events;
using Sugarista.Api.Authorization;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Constants;
using Sugarista.Infrastructure.Logging;
using Sugarista.Infrastructure.Settings;

namespace Sugarista.Api.Controllers;

/// <summary>
/// Admin view + control of the logging pipeline: current minimum level,
/// queue pressure, dropped-event counter, on-disk usage and retention.
/// The level can be changed at runtime (persisted in app-setup.json) —
/// no rebuild, no restart.
/// </summary>
[Authorize]
[Route("api/admin/logging")]
public class AdminLoggingController(
    LoggingDiagnostics diagnostics,
    AppSetupStore appSetupStore,
    IAuditLog audit) : ApiControllerBase
{
    public sealed record LoggingStatusResponse(
        string MinimumLevel,
        bool FileSinkEnabled,
        long DroppedEvents,
        long AcceptedEvents,
        int QueueDepth,
        int QueueCapacity,
        long LogDirectoryBytes,
        int LogFileCount,
        int FileSizeLimitMb,
        int RetainedFiles,
        bool SeqEnabled,
        string? SeqUrl);

    public sealed record SetLevelRequest(string Level);

    public sealed record SetLevelResponse(
        string MinimumLevel,
        string Message);

    private static readonly string[] AllowedLevels =
        ["Verbose", "Debug", "Information", "Warning", "Error", "Fatal"];

    [HttpGet]
    [RequirePermission(AdminPermissions.SiteSettings.View)]
    public ActionResult<LoggingStatusResponse> Get()
    {
        var (dropped, accepted, depth) = diagnostics.QueueStats;
        var (bytes, files) = diagnostics.DiskUsage;

        return Ok(new LoggingStatusResponse(
            diagnostics.CurrentMinimumLevel,
            diagnostics.FileSinkEnabled,
            dropped,
            accepted,
            depth,
            diagnostics.QueueCapacity,
            bytes,
            files,
            diagnostics.FileSizeLimitMb,
            diagnostics.RetainedFiles,
            diagnostics.SeqEnabled,
            diagnostics.SeqUrl));
    }

    [HttpPut("level")]
    [RequirePermission(AdminPermissions.SiteSettings.Update)]
    public ActionResult<SetLevelResponse> SetLevel([FromBody] SetLevelRequest request)
    {
        var normalized = char.ToUpperInvariant(request.Level?[0] ?? ' ') +
                         (request.Level?.Length > 1 ? request.Level[1..].ToLowerInvariant() : string.Empty);

        if (string.IsNullOrWhiteSpace(request.Level) ||
            !AllowedLevels.Contains(normalized, StringComparer.Ordinal))
        {
            return BadRequest(new
            {
                success = false,
                message = $"سطح نامعتبر است. مجاز: {string.Join(", ", AllowedLevels)}"
            });
        }

        diagnostics.LevelSwitch.MinimumLevel = Enum.Parse<LogEventLevel>(normalized);
        appSetupStore.PatchLoggingMinimumLevel(normalized); // survives restarts/rebuilds

        audit.Write(
            "logging.level_changed",
            $"minimum level → {normalized}");

        return Ok(new SetLevelResponse(
            diagnostics.CurrentMinimumLevel,
            $"سطح لاگ روی {normalized} تنظیم شد (بدون ری‌استارت، در فایل پیکربندی ذخیره شد)."));
    }
}
