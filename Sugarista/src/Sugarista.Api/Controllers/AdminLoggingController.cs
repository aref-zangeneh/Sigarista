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

    /// <summary>Bulk file deletion payload (with the disk space it freed).</summary>
    public sealed record BatchDeleteResponse(
        int DeletedFiles,
        long FreedBytes,
        string Message);

    /// <summary>Bulk entry deletion payload (oldest N entries of one file).</summary>
    public sealed record EntryDeleteResponse(
        int DeletedEntries,
        long FreedBytes,
        bool FileDeleted,
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
    /// Bulk delete: removes the N oldest log files in one go (e.g. packs of
    /// 100–500 for very chatty setups). Today's files — the ones the writer
    /// is still appending to — are always kept. Useful when a misbehaving
    /// day produced far more files than the viewer lists comfortably.
    /// </summary>
    [HttpDelete("files/batch")]
    [RequirePermission(AdminPermissions.SystemLogs.Delete)]
    public ActionResult<BatchDeleteResponse> DeleteOldestFilesBatch([FromQuery] int count = 100)
    {
        count = Math.Clamp(count, 1, 500);
        var result = logFileStore.DeleteOldestFiles(count);

        audit.Write(
            "system_log_files_batch_deleted",
            $"batch removed {result.DeletedFiles} oldest log file(s), freed {result.FreedBytes} bytes");

        return Ok(new BatchDeleteResponse(
            result.DeletedFiles,
            result.FreedBytes,
            result.DeletedFiles == 0
                ? "فایل قدیمی‌ای برای حذف نبود (فایل‌های امروز حفظ می‌شوند)."
                : $"{result.DeletedFiles} فایل قدیمی‌ترین حذف شد و {FormatBytes(result.FreedBytes)} فضا آزاد شد."));
    }

    /// <summary>
    /// Bulk entry deletion: removes the N OLDEST entries inside one log file
    /// (e.g. 100–500 at a time) by rewriting the file without their lines.
    /// Only files of a past day can be trimmed — today's file is still being
    /// written to and cannot be safely rewritten.
    /// </summary>
    [HttpDelete("files/{fileName}/entries/oldest")]
    [RequirePermission(AdminPermissions.SystemLogs.Delete)]
    public ActionResult<EntryDeleteResponse> DeleteOldestEntries(string fileName, [FromQuery] int count = 100)
    {
        count = Math.Clamp(count, 1, 500);

        var result = logFileStore.DeleteOldestEntries(fileName, count);
        if (!result.Success)
        {
            return result.Reason switch
            {
                "not-found" => NotFound(new { success = false, message = "فایل لاگ پیدا نشد." }),
                "active-day" => BadRequest(new
                {
                    success = false,
                    message = "فایل امروز در حال نوشتن است و نمی‌توان ردیف‌هایش را حذف کرد؛ فایل‌های روزهای قبل قابل حذف دسته‌ای هستند."
                }),
                _ => StatusCode(500, new { success = false, message = "حذف ردیف‌ها انجام نشد (خطای دیسک)." }),
            };
        }

        audit.Write(
            "system_log_entries_deleted",
            $"removed {result.DeletedEntries} oldest entries from {fileName}, freed {result.FreedBytes} bytes" +
            (result.FileDeleted ? " (file removed entirely)" : string.Empty));

        var message = result.FileDeleted
            ? $"همهٔ ردیف‌های فایل «{fileName}» ({result.DeletedEntries} ردیف) حذف شد و {FormatBytes(result.FreedBytes)} فضا آزاد شد."
            : $"{result.DeletedEntries} ردیف قدیمی‌ترین حذف شد و {FormatBytes(result.FreedBytes)} فضا آزاد شد.";

        return Ok(new EntryDeleteResponse(result.DeletedEntries, result.FreedBytes, result.FileDeleted, message));
    }

    /// <summary>
    /// Deletes every log file of previous days (today's files — including any
    /// size-rolled parts of today — stay, because the writer keeps appending
    /// to them). Frees space taken by older days.
    /// </summary>
    [HttpDelete("files")]
    [RequirePermission(AdminPermissions.SystemLogs.Delete)]
    public ActionResult<DeleteResponse> DeleteOldFiles()
    {
        var removed = logFileStore.DeleteAllExceptToday();

        audit.Write(
            "system_log_files_cleared",
            $"removed {removed} old log file(s), kept all of today's files");

        return Ok(new DeleteResponse(
            removed,
            removed == 0
                ? "فایل قدیمی‌ای برای حذف نبود."
                : $"{removed} فایل قدیمی حذف شد؛ فایل‌های امروز حفظ شدند."));
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0)
        {
            return "0 KB";
        }

        return bytes < 1024 * 1024
            ? $"{bytes / 1024.0:0} کیلوبایت"
            : $"{bytes / (1024.0 * 1024.0):0.#} مگابایت";
    }
}
