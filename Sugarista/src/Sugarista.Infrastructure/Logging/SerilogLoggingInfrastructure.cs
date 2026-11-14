using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Sugarista.Infrastructure.Settings;

namespace Sugarista.Infrastructure.Logging;

/// <summary>
/// The complete lightweight logging infrastructure, created once at startup:
///
///   • Serilog engine, ILogger&lt;T&gt; everywhere (no static Log usage).
///   • Console sink (Docker logs) + rolling compact-JSON file sink inside a
///     dedicated volume — 20 MB × 7 files by default, configurable.
///   • The file write is asynchronous through a BOUNDED queue (BoundedDroppingSink):
///     a full queue DROPS events and counts them — it never blocks or fails
///     a request, and the drop count is observable via the admin diagnostics.
///   • Identical consecutive repeats are collapsed (RepeatSuppressingSink).
///   • Sensitive property names are redacted before any sink sees them.
///   • Seq is OPTIONAL: enabled purely from configuration; a dead Seq can
///     never affect the API (sink failures are swallowed by Serilog).
///   • Audit events go through a separate always-flushed file (FileAuditLog).
///   • Framework noise (Microsoft/System, EF command logging) is filtered.
///   • The minimum level is a LoggingLevelSwitch: changeable at runtime from
///     the admin panel (persisted in app-setup.json) — no rebuild needed.
/// </summary>
public sealed class SerilogLoggingInfrastructure
{
    public ILogger Logger { get; private init; } = null!;

    public LoggingLevelSwitch LevelSwitch { get; private init; } = null!;

    public LoggingDiagnostics Diagnostics { get; private init; } = null!;

    public FileAuditLog Audit { get; private init; } = null!;

    private bool _shutdownDone;

    /// <summary>Flushes buffers on normal shutdown; safe to call once.</summary>
    public void Shutdown()
    {
        if (_shutdownDone)
        {
            return;
        }

        _shutdownDone = true;
        try
        {
            Diagnostics.FileSink?.Flush(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // Flushing is best-effort by definition.
        }

        Audit.Dispose();
        (Logger as IDisposable)?.Dispose();
    }

    public static SerilogLoggingInfrastructure Create(
        IConfiguration configuration,
        IHostEnvironment environment,
        AppSetupStore appSetupStore)
    {
        var options = configuration.GetSection(LoggingInfrastructureOptions.SectionName)
            .Get<LoggingInfrastructureOptions>() ?? new LoggingInfrastructureOptions();

        var resolvedLogDir = Path.IsPathRooted(options.FileSinkPath)
            ? Path.GetDirectoryName(options.FileSinkPath)!
            : Path.GetDirectoryName(Path.Combine(environment.ContentRootPath, options.FileSinkPath))!;

        // Runtime level: app-setup.json (admin panel change) wins over appsettings.
        var initialLevel = appSetupStore.Load().LoggingMinimumLevel ?? options.MinimumLevel;
        var levelSwitch = new LoggingLevelSwitch(ParseLevel(initialLevel, LogEventLevel.Information));

        Directory.CreateDirectory(resolvedLogDir);

        // ── File sink (compact JSON, rolling, size-limited) ──────────────────
        BoundedDroppingSink? fileSink = null;
        if (options.FileSinkEnabled)
        {
            var fileLogger = new LoggerConfiguration()
                .MinimumLevel.Is(LogEventLevel.Verbose) // the pipeline-level switch already filtered
                .Enrich.With(new SensitiveDataEnricher())
                .WriteTo.File(
                    new CompactJsonFormatter(),
                    options.FileSinkPath,
                    rollingInterval: RollingInterval.Day,
                    fileSizeLimitBytes: Math.Max(1, options.FileSinkSizeLimitMb) * 1024 * 1024,
                    rollOnFileSizeLimit: true,
                    retainedFileCountLimit: Math.Max(1, options.FileSinkRetainedFiles),
                    buffered: true, // the inner FileStream buffers; our queue bounds the events
                    shared: false)
                .CreateLogger();

            fileSink = new BoundedDroppingSink(fileLogger, options.QueueCapacity);
        }

        // ── Console sink (docker logs) ───────────────────────────────────────
        Logger? consoleLogger = null;
        if (options.ConsoleEnabled)
        {
            consoleLogger = new LoggerConfiguration()
                .MinimumLevel.Is(LogEventLevel.Verbose)
                .Enrich.With(new SensitiveDataEnricher())
                .WriteTo.Console(
                    outputTemplate:
                    "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                    standardErrorFromLevel: LogEventLevel.Warning)
                .CreateLogger();
        }

        // ── Aggregate: console + (dedup →) bounded-file + optional Seq ────────
        var targets = new List<ILogEventSink>();
        if (consoleLogger is not null)
        {
            targets.Add(consoleLogger);
        }

        if (fileSink is not null)
        {
            // Dedup wraps only the file target: the console stays human-friendly
            // while the persisted log stays compact and deduplicated.
            targets.Add(new RepeatSuppressingSink(fileSink, TimeSpan.FromSeconds(options.RepeatWindowSeconds)));
        }

        if (options.SeqEnabled && !string.IsNullOrWhiteSpace(options.SeqUrl))
        {
            // Optional by design. Sink failures (Seq down) are swallowed by
            // Serilog — the API is never affected.
            var seqLogger = new LoggerConfiguration()
                .MinimumLevel.Is(LogEventLevel.Verbose)
                .Enrich.With(new SensitiveDataEnricher())
                .WriteTo.Seq(options.SeqUrl, batchPostingLimit: 100, period: TimeSpan.FromSeconds(5))
                .CreateLogger();
            targets.Add(seqLogger);
        }

        var aggregate = new AggregateSink(targets);

        var logger = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(levelSwitch)
            // Framework noise: the biggest log producers by far.
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            // SQL query logging OFF by default (production). In Development it
            // stays Information so developers can see queries locally.
            .MinimumLevel.Override(
                "Microsoft.EntityFrameworkCore.Database.Command",
                environment.IsDevelopment() ? LogEventLevel.Information : LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("App", "Sugarista.Api")
            .WriteTo.Sink(aggregate)
            .CreateLogger();

        Serilog.Debugging.SelfLog.Enable(message => Console.Error.WriteLine($"[serilog-selflog] {message}"));

        var audit = new FileAuditLog(
            options.AuditEnabled, options.AuditPath, options.AuditSizeLimitMb, options.AuditRetainedFiles, environment);

        return new SerilogLoggingInfrastructure
        {
            Logger = logger,
            LevelSwitch = levelSwitch,
            Audit = audit,
            Diagnostics = new LoggingDiagnostics(
                fileSink,
                levelSwitch,
                resolvedLogDir,
                options.FileSinkEnabled,
                options.FileSinkSizeLimitMb,
                options.FileSinkRetainedFiles,
                options.QueueCapacity,
                options.SeqEnabled,
                options.SeqUrl)
        };
    }

    private static LogEventLevel ParseLevel(string? value, LogEventLevel fallback)
    {
        return Enum.TryParse<LogEventLevel>(value, ignoreCase: true, out var parsed)
            ? parsed
            : fallback;
    }

    private sealed class AggregateSink : ILogEventSink
    {
        private readonly IReadOnlyList<ILogEventSink> _targets;

        public AggregateSink(IReadOnlyList<ILogEventSink> targets) => _targets = targets;

        public void Emit(LogEvent logEvent)
        {
            foreach (var target in _targets)
            {
                try
                {
                    target.Emit(logEvent);
                }
                catch
                {
                    // One failing target never breaks the others.
                }
            }
        }
    }
}
