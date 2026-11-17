using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Sugarista.Infrastructure.Logging;

/// <summary>One browsable log entry as served to the admin panel.</summary>
public sealed record SystemLogEntry(
    DateTime TimestampUtc,
    string Level,
    string Category,
    string? Source,
    string Message,
    string? Exception,
    IReadOnlyDictionary<string, string> Properties);

/// <summary>Per-file summary for the file list: size, write time and level distribution.</summary>
public sealed record SystemLogFileSummary(
    string FileName,
    long SizeBytes,
    DateTime LastWriteUtc,
    IReadOnlyDictionary<string, int> LevelCounts);

/// <summary>A filtered, paginated slice of one log file plus its aggregate stats.</summary>
public sealed record SystemLogEntriesResult(
    string FileName,
    IReadOnlyList<SystemLogEntry> Entries,
    int TotalMatching,
    int Page,
    int PageSize,
    IReadOnlyDictionary<string, int> LevelCounts,
    IReadOnlyList<string> Categories);

/// <summary>Outcome of a bulk file deletion (admin batch or retention job).</summary>
public sealed record SystemLogBatchDeleteResult(int DeletedFiles, long FreedBytes);

/// <summary>Outcome of deleting the oldest N entries inside one log file.</summary>
/// <param name="Success">False when nothing could be deleted (see Reason).</param>
/// <param name="DeletedEntries">Entries actually removed.</param>
/// <param name="FreedBytes">Disk bytes the operation freed.</param>
/// <param name="FileDeleted">True when the file itself was removed (count ≥ all entries).</param>
/// <param name="Reason">
/// Machine-readable failure cause: "not-found", "active-day" (the writer is
/// still appending to that day's file — rewriting it is unsafe), "io-error".
/// </param>
public sealed record SystemLogEntryDeleteResult(
    bool Success,
    int DeletedEntries,
    long FreedBytes,
    bool FileDeleted,
    string? Reason = null);

/// <summary>
/// Reads the rolling Serilog compact-JSON files (sugarista-*.log) written by
/// <see cref="SerilogLoggingInfrastructure"/> and turns them into browsable,
/// categorized entries. Also deletes files on admin request.
///
///   • Filenames are strictly validated (no path traversal).
///   • Parsed entries are cached per file and invalidated by size/mtime, so
///     pagination and the file list never re-parse the same file version.
///   • Parsing is bounded (MaxLinesPerFile) to keep memory in check.
/// </summary>
public sealed partial class SystemLogFileStore(LoggingDiagnostics diagnostics)
{
    private const int MaxLinesPerFile = 300_000;
    private const int MaxPropertyValueLength = 500;

    private static readonly Regex FileNamePattern = SystemLogFileNameRegex();

    private readonly ConcurrentDictionary<string, ParsedFile> _cache = new(StringComparer.Ordinal);

    /// <summary>Log files, newest write first.</summary>
    public IReadOnlyList<SystemLogFileSummary> ListFiles()
    {
        var directory = diagnostics.LogDirectory;
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var summaries = new List<SystemLogFileSummary>();
        foreach (var path in Directory.EnumerateFiles(directory, "sugarista-*.log"))
        {
            var fileName = Path.GetFileName(path);
            if (!FileNamePattern.IsMatch(fileName))
            {
                continue;
            }

            var info = new FileInfo(path);
            var parsed = GetOrParse(fileName, path, info);
            summaries.Add(new SystemLogFileSummary(
                fileName,
                info.Length,
                info.LastWriteTimeUtc,
                parsed.LevelCounts));
        }

        return summaries.OrderByDescending(file => file.LastWriteUtc).ToList();
    }

    /// <summary>
    /// A filtered page of entries from one file, newest first, plus level/category
    /// aggregates of the whole file (for filter chips).
    /// </summary>
    public SystemLogEntriesResult ReadEntries(
        string fileName,
        string? level = null,
        string? category = null,
        string? search = null,
        int page = 1,
        int pageSize = 50)
    {
        var (path, parsed) = Load(fileName);

        IEnumerable<SystemLogEntry> filtered = parsed.Entries;

        if (!string.IsNullOrWhiteSpace(level))
        {
            filtered = filtered.Where(entry => string.Equals(entry.Level, level, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            filtered = filtered.Where(entry => string.Equals(entry.Category, category, StringComparison.Ordinal));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var needle = search.Trim();
            filtered = filtered.Where(entry =>
                (entry.Message?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (entry.Exception?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (entry.Source?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        var matching = filtered as IReadOnlyList<SystemLogEntry> ?? filtered.ToList();
        var total = matching.Count;

        var boundedPage = Math.Max(1, page);
        var boundedPageSize = Math.Clamp(pageSize, 1, 200);
        var skip = (boundedPage - 1) * boundedPageSize;

        // Reverse FIRST, then page — so page 1 starts at the newest entry overall.
        var pageEntries = matching
            .Reverse()
            .Skip(skip)
            .Take(boundedPageSize)
            .ToList();

        return new SystemLogEntriesResult(
            fileName,
            pageEntries,
            total,
            boundedPage,
            boundedPageSize,
            parsed.LevelCounts,
            parsed.Categories);
    }

    /// <summary>Deletes one log file. Returns false when it does not exist or could not be removed.</summary>
    public bool DeleteFile(string fileName)
    {
        var path = ResolvePath(fileName);
        if (path is null || !File.Exists(path))
        {
            return false;
        }

        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }

        _cache.TryRemove(Path.GetFileName(path), out _);
        return true;
    }

    /// <summary>
    /// Deletes every log file except the given one (typically today's active
    /// file, which the writer keeps appending to). Returns how many were removed.
    /// </summary>
    public int DeleteAllExcept(string keepFileName)
    {
        var directory = diagnostics.LogDirectory;
        if (!Directory.Exists(directory))
        {
            return 0;
        }

        var removed = 0;
        foreach (var path in Directory.EnumerateFiles(directory, "sugarista-*.log").ToList())
        {
            var fileName = Path.GetFileName(path);
            if (!FileNamePattern.IsMatch(fileName) ||
                string.Equals(fileName, keepFileName, StringComparison.Ordinal))
            {
                continue;
            }

            if (DeleteFile(fileName))
            {
                removed++;
            }
        }

        return removed;
    }

    /// <summary>
    /// Extracts the day encoded in a log file name (sugarista-YYYYMMDD*.log).
    /// Returns false for names without a parsable day part.
    /// </summary>
    public static bool TryGetFileDate(string fileName, out DateTime dateUtc)
    {
        dateUtc = DateTime.MinValue;
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        var match = DatePartRegex().Match(fileName);
        if (!match.Success ||
            !DateTime.TryParseExact(
                match.Value,
                "yyyyMMdd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out dateUtc))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Deletes the N oldest log files in one batch (the admin panel's bulk
    /// delete). Files of the CURRENT day are always skipped — the Serilog
    /// writer still holds them open, and deleting an open file on Linux
    /// frees no space until restart while silently orphaning new writes.
    /// Returns how many were removed and how many bytes that freed.
    /// </summary>
    public SystemLogBatchDeleteResult DeleteOldestFiles(int count)
    {
        count = Math.Max(1, count);

        var today = DateTime.UtcNow.Date;
        var candidates = ListFiles()
            .Where(file => !(TryGetFileDate(file.FileName, out var day) && day >= today))
            .OrderBy(file => file.LastWriteUtc)
            .Take(count)
            .ToList();

        var removed = 0;
        long freed = 0;
        foreach (var candidate in candidates)
        {
            if (DeleteFile(candidate.FileName))
            {
                removed++;
                freed += candidate.SizeBytes;
            }
        }

        return new SystemLogBatchDeleteResult(removed, freed);
    }

    /// <summary>
    /// Deletes every log file older than the given number of days (measured
    /// from the day encoded in the file name). Used by the scheduled retention
    /// job; safe to call at any frequency. Files that are still today's are
    /// naturally excluded by the cutoff.
    /// </summary>
    public SystemLogBatchDeleteResult DeleteOlderThanDays(int days)
    {
        if (days <= 0)
        {
            return new SystemLogBatchDeleteResult(0, 0);
        }

        var cutoff = DateTime.UtcNow.Date.AddDays(-days);
        var directory = diagnostics.LogDirectory;
        if (!Directory.Exists(directory))
        {
            return new SystemLogBatchDeleteResult(0, 0);
        }

        var removed = 0;
        long freed = 0;
        foreach (var path in Directory.EnumerateFiles(directory, "sugarista-*.log").ToList())
        {
            var fileName = Path.GetFileName(path);
            if (!FileNamePattern.IsMatch(fileName) ||
                !TryGetFileDate(fileName, out var day) ||
                day >= cutoff)
            {
                continue;
            }

            var size = new FileInfo(path).Length;
            if (DeleteFile(fileName))
            {
                removed++;
                freed += size;
            }
        }

        return new SystemLogBatchDeleteResult(removed, freed);
    }

    /// <summary>
    /// Deletes every log file whose name does not encode TODAY's day — keeps
    /// all of today's files including any size-rolled parts of them.
    /// Returns how many files were removed.
    /// </summary>
    public int DeleteAllExceptToday()
    {
        var today = DateTime.UtcNow.Date;
        var directory = diagnostics.LogDirectory;
        if (!Directory.Exists(directory))
        {
            return 0;
        }

        var removed = 0;
        foreach (var path in Directory.EnumerateFiles(directory, "sugarista-*.log").ToList())
        {
            var fileName = Path.GetFileName(path);
            if (!FileNamePattern.IsMatch(fileName) ||
                (TryGetFileDate(fileName, out var day) && day >= today))
            {
                continue;
            }

            if (DeleteFile(fileName))
            {
                removed++;
            }
        }

        return removed;
    }

    /// <summary>
    /// Deletes the oldest N entries INSIDE one closed log file by rewriting it
    /// without their lines (the admin panel's entry-level batch delete, e.g.
    /// packs of 100–500). The file's remaining lines are preserved byte-for-byte.
    ///
    /// Guards:
    ///   • Only files of a PAST day are eligible — today's file is held open by
    ///     the Serilog writer, and rewriting an open file loses new appends.
    ///   • If the request covers every entry, the whole (closed) file is
    ///     deleted instead — same effect, simpler and fully frees the space.
    /// Returns false results for unknown files, today's file or I/O errors.
    /// </summary>
    public SystemLogEntryDeleteResult DeleteOldestEntries(string fileName, int count)
    {
        var path = ResolvePath(fileName);
        if (path is null || !File.Exists(path))
        {
            return new SystemLogEntryDeleteResult(false, 0, 0, false, "not-found");
        }

        if (TryGetFileDate(fileName, out var day) && day >= DateTime.UtcNow.Date)
        {
            return new SystemLogEntryDeleteResult(false, 0, 0, false, "active-day");
        }

        count = Math.Max(1, count);

        var info = new FileInfo(path);
        var originalSize = info.Length;
        var totalEntries = Load(fileName).Parsed.Entries.Count;

        if (totalEntries == 0)
        {
            _cache.TryRemove(fileName, out _);
            return new SystemLogEntryDeleteResult(true, 0, 0, false);
        }

        if (count >= totalEntries)
        {
            // Every entry is going away — deleting the closed file outright
            // is equivalent, simpler and frees the whole size.
            if (!DeleteFile(fileName))
            {
                return new SystemLogEntryDeleteResult(false, 0, 0, false, "io-error");
            }

            return new SystemLogEntryDeleteResult(true, totalEntries, originalSize, true);
        }

        var tempPath = path + ".tmp";
        try
        {
            // Single pass: keep every raw line except the first `count` parsed
            // entry lines. Blank and unparseable lines are kept as-is, so
            // nothing outside the counted entries is ever touched.
            var kept = new List<string>(capacity: totalEntries - count + 16);
            var entryIndex = 0;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
            {
                while (reader.ReadLine() is { } line)
                {
                    if (line.Length > 0 && TryParseLine(line) is not null)
                    {
                        entryIndex++;
                        if (entryIndex <= count)
                        {
                            continue; // this is one of the oldest entries — drop it
                        }
                    }

                    kept.Add(line);
                }
            }

            File.WriteAllLines(tempPath, kept);
            // Rename over the original: atomic on POSIX, keeps the file name
            // (and therefore the store's validation regex) intact.
            File.Move(tempPath, path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            try
            {
                File.Delete(tempPath);
            }
            catch
            {
                // best effort — the temp file lives next to the log anyway
            }

            return new SystemLogEntryDeleteResult(false, 0, 0, false, "io-error");
        }

        var newSize = new FileInfo(path).Length;
        _cache.TryRemove(fileName, out _);
        return new SystemLogEntryDeleteResult(true, count, Math.Max(0, originalSize - newSize), false);
    }

    private (string Path, ParsedFile Parsed) Load(string fileName)
    {
        var path = ResolvePath(fileName)
            ?? throw new FileNotFoundException("Log file was not found.", fileName);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Log file was not found.", fileName);
        }

        var info = new FileInfo(path);
        return (path, GetOrParse(fileName, path, info));
    }

    private ParsedFile GetOrParse(string fileName, string path, FileInfo info)
    {
        var size = info.Exists ? info.Length : 0;
        var stamp = info.Exists ? info.LastWriteTimeUtc : DateTime.MinValue;

        if (_cache.TryGetValue(fileName, out var cached) &&
            cached.SizeBytes == size &&
            cached.LastWriteUtc == stamp)
        {
            return cached;
        }

        var parsed = ParseFile(path);
        parsed.SizeBytes = size;
        parsed.LastWriteUtc = stamp;
        _cache[fileName] = parsed;
        return parsed;
    }

    private string? ResolvePath(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || !FileNamePattern.IsMatch(fileName))
        {
            return null;
        }

        return Path.Combine(diagnostics.LogDirectory, fileName);
    }

    private static ParsedFile ParseFile(string path)
    {
        var entries = new List<SystemLogEntry>();
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);

        var lineIndex = 0;
        while (reader.ReadLine() is { } line)
        {
            lineIndex++;
            if (lineIndex > MaxLinesPerFile)
            {
                break;
            }

            if (line.Length == 0)
            {
                continue;
            }

            var entry = TryParseLine(line);
            if (entry is not null)
            {
                entries.Add(entry);
            }
        }

        var levelCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var categories = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            levelCounts[entry.Level] = levelCounts.GetValueOrDefault(entry.Level) + 1;
            categories.Add(entry.Category);
        }

        return new ParsedFile
        {
            Entries = entries,
            LevelCounts = levelCounts,
            Categories = [.. categories],
        };
    }

    private static SystemLogEntry? TryParseLine(string line)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var timestamp = root.TryGetProperty("@t", out var timestampElement) &&
                            DateTime.TryParse(
                                timestampElement.GetString(),
                                CultureInfo.InvariantCulture,
                                DateTimeStyles.RoundtripKind,
                                out var parsedTimestamp)
                ? DateTime.SpecifyKind(parsedTimestamp, DateTimeKind.Utc)
                : DateTime.MinValue;

            var level = root.TryGetProperty("@l", out var levelElement)
                ? levelElement.GetString() ?? "Information"
                : "Information";

            string? exception = root.TryGetProperty("@x", out var exceptionElement)
                ? exceptionElement.GetString()
                : null;

            var source = root.TryGetProperty("SourceContext", out var sourceElement)
                ? sourceElement.GetString()
                : null;

            var properties = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var property in root.EnumerateObject())
            {
                if (property.Name is "@t" or "@mt" or "@r" or "@l" or "@x" or "SourceContext")
                {
                    continue;
                }

                var value = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString(),
                    JsonValueKind.Number => property.Value.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Null => null,
                    _ => property.Value.GetRawText(),
                };

                if (value is null || value.Length == 0)
                {
                    continue;
                }

                if (value.Length > MaxPropertyValueLength)
                {
                    value = value[..MaxPropertyValueLength] + "…";
                }

                properties[property.Name] = value;
            }

            // Render the message template with its properties — the compact
            // format's @r array only holds parameter renderings, not the text.
            string? message = null;
            if (root.TryGetProperty("@mt", out var templateElement))
            {
                message = RenderTemplate(templateElement.GetString() ?? string.Empty, properties);
            }

            if (string.IsNullOrEmpty(message))
            {
                message = root.TryGetProperty("@r", out var renderings) &&
                          renderings.ValueKind == JsonValueKind.Array &&
                          renderings.GetArrayLength() > 0 &&
                          renderings[0].ValueKind == JsonValueKind.String
                    ? renderings[0].GetString()
                    : null;
            }

            return new SystemLogEntry(
                timestamp,
                level,
                Classify(source),
                source,
                message ?? string.Empty,
                exception,
                properties);
        }
        catch (JsonException)
        {
            return null; // truncated/corrupt line — skip silently
        }
    }

    /// <summary>
    /// Substitutes {Placeholder(:format)} tokens of a Serilog message template
    /// with the entry's property values. Unmatched placeholders are kept as-is.
    /// </summary>
    private static string RenderTemplate(string template, IReadOnlyDictionary<string, string> properties)
    {
        if (template.Length == 0 || !template.Contains('{', StringComparison.Ordinal))
        {
            return template;
        }

        return TemplateHoleRegex().Replace(
            template,
            match =>
            {
                var name = match.Groups[1].Value;
                return properties.TryGetValue(name, out var value) ? value : match.Value;
            });
    }

    /// <summary>Persian category buckets used by the admin viewer's filter.</summary>
    public static string Classify(string? source)    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return "عمومی";
        }

        if (source.Contains("RequestLoggingMiddleware", StringComparison.Ordinal))
        {
            return "درخواست HTTP";
        }

        if (source.StartsWith("Sugarista", StringComparison.Ordinal))
        {
            return "اپلیکیشن";
        }

        if (source.StartsWith("Microsoft.", StringComparison.Ordinal) ||
            source.StartsWith("System.", StringComparison.Ordinal))
        {
            return "فریم‌ورک";
        }

        return "سایر";
    }

    // Size-rolled parts (sugarista-20261117_001.log) are real Serilog roll
    // files and must be listable/deletable too — otherwise they accumulate
    // invisibly while only the "free space" counter sees them.
    [GeneratedRegex(@"^sugarista-\d{8}(_\d{3,})?\.log$", RegexOptions.CultureInvariant)]
    private static partial Regex SystemLogFileNameRegex();

    [GeneratedRegex(@"(?<=sugarista-)\d{8}", RegexOptions.CultureInvariant)]
    private static partial Regex DatePartRegex();

    /// <summary>{Name}, {Name:format}, {@Name}, {@@Name} — no positional holes.</summary>
    [GeneratedRegex(@"\{[@]?([A-Za-z_][A-Za-z0-9_]*)(?::[^}]+)?\}", RegexOptions.CultureInvariant)]
    private static partial Regex TemplateHoleRegex();

    /// <summary>Cache entry for one parsed file version.</summary>
    private sealed class ParsedFile
    {
        public required IReadOnlyList<SystemLogEntry> Entries { get; init; }

        public required IReadOnlyDictionary<string, int> LevelCounts { get; init; }

        public required IReadOnlyList<string> Categories { get; init; }

        public long SizeBytes { get; set; }

        public DateTime LastWriteUtc { get; set; }
    }
}
