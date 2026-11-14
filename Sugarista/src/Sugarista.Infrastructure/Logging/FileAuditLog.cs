using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Sugarista.Application.Common.Interfaces;

namespace Sugarista.Infrastructure.Logging;

/// <summary>
/// File-backed audit log with an independent reliability policy: a dedicated
/// Serilog logger writing DIRECTLY (synchronous, unbuffered) to its own
/// rolling file. Regular log events may be dropped under pressure by design;
/// audit events never are. Failures are swallowed — an audit write must
/// never break the request that caused it.
/// </summary>
public sealed class FileAuditLog : IAuditLog, IDisposable
{
    private readonly ILogger? _logger;

    public FileAuditLog(bool enabled, string path, int sizeLimitMb, int retainedFiles, IHostEnvironment environment)
    {
        if (!enabled)
        {
            return;
        }

        try
        {
            var resolved = Path.IsPathRooted(path)
                ? path
                : Path.Combine(environment.ContentRootPath, path);

            var directory = Path.GetDirectoryName(resolved);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _logger = new LoggerConfiguration()
                .MinimumLevel.Is(LogEventLevel.Information)
                .Enrich.WithProperty("AuditSource", "sugarista")
                .WriteTo.File(
                    new Serilog.Formatting.Compact.CompactJsonFormatter(),
                    resolved,
                    rollingInterval: Serilog.RollingInterval.Day,
                    fileSizeLimitBytes: Math.Max(1, sizeLimitMb) * 1024 * 1024,
                    rollOnFileSizeLimit: true,
                    retainedFileCountLimit: Math.Max(1, retainedFiles),
                    buffered: false, // direct write: nothing buffered that could be lost
                    shared: false)
                .CreateLogger();
        }
        catch
        {
            // Disk trouble must not take the site down; audit just stays off.
            _logger = null;
        }
    }

    public void Write(string action, string? detail = null, object? data = null)
    {
        if (_logger is null)
        {
            return;
        }

        try
        {
            _logger.Information(
                "AUDIT {AuditAction} {AuditDetail} {@AuditData}",
                action,
                detail ?? string.Empty,
                data);
        }
        catch
        {
            // Never throw from the audit path.
        }
    }

    public void Dispose() => (_logger as IDisposable)?.Dispose();
}
