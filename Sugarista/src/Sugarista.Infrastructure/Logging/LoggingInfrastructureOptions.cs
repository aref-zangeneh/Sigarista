namespace Sugarista.Infrastructure.Logging;

/// <summary>
/// Every knob of the logging infrastructure. Bound once at startup from the
/// <c>LoggingInfrastructure</c> configuration section, so any value can be
/// changed on the server via environment variables without a rebuild:
/// <c>LoggingInfrastructure__FileSink__RetainedFiles=14</c>.
/// </summary>
public sealed class LoggingInfrastructureOptions
{
    public const string SectionName = "LoggingInfrastructure";

    /// <summary>Initial minimum level. The admin panel can change it at runtime
    /// (persisted in app-setup.json), which wins over this value.</summary>
    public string MinimumLevel { get; set; } = "Information";

    public bool ConsoleEnabled { get; set; } = true;

    public bool FileSinkEnabled { get; set; } = true;

    /// <summary>Path prefix; the rolling scheme appends the date/sequence.
    /// Relative paths resolve against the content root (/app/logs in Docker).</summary>
    public string FileSinkPath { get; set; } = "logs/sugarista-.log";

    /// <summary>Roll file when it reaches this size. Default 20 MB.</summary>
    public int FileSinkSizeLimitMb { get; set; } = 20;

    /// <summary>How many rolled files to keep. Default 7 → ≈140 MB worst case.</summary>
    public int FileSinkRetainedFiles { get; set; } = 7;

    /// <summary>Bounded queue between the request thread and the file writer.
    /// Full queue ⇒ events are DROPPED and counted, never blocking requests.
    /// Default 10,000 events (≈ a few MB of RAM at most, never unbounded).</summary>
    public int QueueCapacity { get; set; } = 10_000;

    public bool SeqEnabled { get; set; }

    public string? SeqUrl { get; set; }

    /// <summary>Separate always-flushed file for audit events (never dropped).
    /// Audit is low-frequency, so direct synchronous writes are fine there.</summary>
    public bool AuditEnabled { get; set; } = true;

    public string AuditPath { get; set; } = "logs/audit-.log";

    public int AuditSizeLimitMb { get; set; } = 5;

    public int AuditRetainedFiles { get; set; } = 14;

    /// <summary>Repeat-suppression window: identical consecutive events inside
    /// this window are collapsed into one + an aggregate count.</summary>
    public int RepeatWindowSeconds { get; set; } = 2;
}
