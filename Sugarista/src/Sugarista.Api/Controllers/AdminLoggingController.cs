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
/// no rebuild, no restart. The on-disk log files are also browsable here:
/// categorized, filterable, paginated entries plus safe file deletion.
/// </summary>
[Authorize]
[Route("api/admin/logging")]
public class AdminLoggingController(
    LoggingDiagnostics diagnostics,
    SystemLogFileStore logFileStore,
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

    // ── Browsing + managing the on-disk log files ────────────────────────────

    /// <summary>Deletion confirmation payload.</summary>
    public sealed record DeleteResponse(
        int DeletedFiles,
        string Message);

    /// <summary>
    /// All system log files, newest write first, each with its size and
    /// level distribution (for filter chips in the viewer).
    /// </summary>
    [HttpGet("files")]
    [RequirePermission(AdminPermissions.SystemLogs.View)]
    public ActionResult<IReadOnlyList<SystemLogFileSummary>> GetFiles()
    {
        return Ok(logFileStore.ListFiles());
    }

    /// <summary>
    /// A categorized, filtered and paginated slice of one log file (newest first).
    /// level: Verbose/Debug/Information/Warning/Error/Fatal — empty = all.
    /// category: one of the aggregated categories — empty = all.
    /// search: substring match on message, exception and source.
    /// </summary>
    [HttpGet("files/{fileName}/entries")]
    [RequirePermission(AdminPermissions.SystemLogs.View)]
    public ActionResult<SystemLogEntriesResult> GetEntries(
        string fileName,
        [FromQuery] string? level,
        [FromQuery] string? category,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            return Ok(logFileStore.ReadEntries(fileName, level, category, search, page, pageSize));
        }
        catch (FileNotFoundException)
        {
            return NotFound(new { success = false, message = "فایل لاگ پیدا نشد (شاید قبلاً حذف شده باشد)." });
        }
    }

    /// <summary>Deletes one log file permanently (disk space is freed immediately).</summary>
    [HttpDelete("files/{fileName}")]
    [RequirePermission(AdminPermissions.SystemLogs.Delete)]
    public ActionResult<DeleteResponse> DeleteFile(string fileName)
    {
        if (!logFileStore.DeleteFile(fileName))
        {
            return NotFound(new { success = false, message = "فایل لاگ پیدا نشد یا حذف نشد." });
        }

        audit.Write(
            "system_log_file_deleted",
            $"log file deleted: {fileName}");

        return Ok(new DeleteResponse(1, $"فایل «{fileName}» حذف شد."));
    }

    /// <summary>
    /// Deletes every log file except the current one (today's file stays, because
    /// the writer keeps appending to it). Frees space taken by older days.
    /// </summary>
    [HttpDelete("files")]
    [RequirePermission(AdminPermissions.SystemLogs.Delete)]
    public ActionResult<DeleteResponse> DeleteOldFiles()
    {
        var today = $"sugarista-{DateTime.UtcNow:yyyyMMdd}.log";
        var removed = logFileStore.DeleteAllExcept(today);

        audit.Write(
            "system_log_files_cleared",
            $"removed {removed} old log file(s), kept {today}");

        return Ok(new DeleteResponse(
            removed,
            removed == 0
                ? "فایل قدیمی‌ای برای حذف نبود."
                : $"{removed} فایل قدیمی حذف شد؛ فایل امروز ({today}) حفظ شد."));
    }
}
