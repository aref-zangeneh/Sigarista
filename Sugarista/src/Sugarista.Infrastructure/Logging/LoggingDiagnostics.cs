using Serilog.Core;

namespace Sugarista.Infrastructure.Logging;

/// <summary>
/// Live view of the logging pipeline for the admin panel: queue pressure,
/// dropped events, current level, on-disk usage and retention settings.
/// Cheap to call (no I/O except a directory listing when requested).
/// </summary>
public sealed class LoggingDiagnostics
{
    public LoggingDiagnostics(
        BoundedDroppingSink? fileSink,
        LoggingLevelSwitch levelSwitch,
        string logDirectory,
        bool fileSinkEnabled,
        int fileSizeLimitMb,
        int retainedFiles,
        int queueCapacity,
        bool seqEnabled,
        string? seqUrl)
    {
        FileSink = fileSink;
        LevelSwitch = levelSwitch;
        LogDirectory = logDirectory;
        FileSinkEnabled = fileSinkEnabled;
        FileSizeLimitMb = fileSizeLimitMb;
        RetainedFiles = retainedFiles;
        QueueCapacity = queueCapacity;
        SeqEnabled = seqEnabled;
        SeqUrl = seqUrl;
    }

    public BoundedDroppingSink? FileSink { get; }

    public LoggingLevelSwitch LevelSwitch { get; }

    public string LogDirectory { get; }

    public bool FileSinkEnabled { get; }

    public int FileSizeLimitMb { get; }

    public int RetainedFiles { get; }

    public int QueueCapacity { get; }

    public bool SeqEnabled { get; }

    public string? SeqUrl { get; }

    public string CurrentMinimumLevel => LevelSwitch.MinimumLevel.ToString();

    public (long Dropped, long Accepted, int QueueDepth) QueueStats => FileSink is { } sink
        ? (sink.DroppedCount, sink.AcceptedCount, sink.QueueDepth)
        : (0, 0, 0);

    public (long TotalBytes, int FileCount) DiskUsage
    {
        get
        {
            try
            {
                var directory = new DirectoryInfo(LogDirectory);
                if (!directory.Exists)
                {
                    return (0, 0);
                }

                var files = directory.GetFiles("*.log", SearchOption.TopDirectoryOnly);
                return (files.Sum(file => file.Length), files.Length);
            }
            catch
            {
                return (0, 0);
            }
        }
    }
}
