using Sugarista.Infrastructure.Logging;

namespace Sugarista.Infrastructure.UnitTests;

/// <summary>
/// Tests for the admin-panel log file browser: parsing compact-JSON Serilog
/// files, categorization, filtering, pagination and safe deletion.
/// </summary>
public sealed class SystemLogFileStoreTests : IDisposable
{
    private readonly string _directory;
    private readonly LoggingDiagnostics _diagnostics;
    private readonly SystemLogFileStore _store;

    public SystemLogFileStoreTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "sgr-logstore-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _diagnostics = new LoggingDiagnostics(
            fileSink: null,
            levelSwitch: new Serilog.Core.LoggingLevelSwitch(),
            logDirectory: _directory,
            fileSinkEnabled: true,
            fileSizeLimitMb: 20,
            retainedFiles: 7,
            queueCapacity: 10_000,
            seqEnabled: false,
            seqUrl: null);
        _store = new SystemLogFileStore(_diagnostics);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private static void WriteLogFile(string directory, string name, params string[] jsonLines)
    {
        var path = Path.Combine(directory, name);
        File.WriteAllLines(path, jsonLines);
    }

    private static string Line(
        string timestamp,
        string level,
        string message,
        string? source = null,
        string? exception = null,
        (string Key, string Value)? extra = null)
    {
        var parts = new List<string> { $"\"@t\":\"{timestamp}\"", $"\"@mt\":\"{message}\"" };
        if (level != "Information")
        {
            parts.Add($"\"@l\":\"{level}\"");
        }

        if (source is not null)
        {
            parts.Add($"\"SourceContext\":\"{source}\"");
        }

        if (exception is not null)
        {
            parts.Add($"\"@x\":\"{exception}\"");
        }

        if (extra is { } property)
        {
            parts.Add($"\"{property.Key}\":\"{property.Value}\"");
        }

        return "{" + string.Join(",", parts) + "}";
    }

    [Fact]
    public void ListFiles_ReturnsNewestFirst_WithLevelCounts()
    {
        WriteLogFile(
            _directory,
            "sugarista-20261116.log",
            Line("2026-11-16T10:00:00.0000000Z", "Information", "ok one"),
            Line("2026-11-16T10:01:00.0000000Z", "Error", "boom", source: "Sugarista.Application.X"));
        WriteLogFile(
            _directory,
            "sugarista-20261117.log",
            Line("2026-11-17T10:00:00.0000000Z", "Warning", "careful"));
        // Audit file must be ignored by the store.
        File.WriteAllText(Path.Combine(_directory, "audit-20261116.log"), "{}");

        var files = _store.ListFiles();

        Assert.Equal(2, files.Count);
        Assert.Equal("sugarista-20261117.log", files[0].FileName);
        Assert.Equal("sugarista-20261116.log", files[1].FileName);

        var older = files.Single(file => file.FileName == "sugarista-20261116.log");
        Assert.Equal(1, older.LevelCounts["Information"]);
        Assert.Equal(1, older.LevelCounts["Error"]);
    }

    [Fact]
    public void ReadEntries_DefaultsMissingLevelToInformation_AndClassifiesCategories()
    {
        WriteLogFile(
            _directory,
            "sugarista-20261117.log",
            Line("2026-11-17T10:00:00.0000000Z", "Information", "GET /api/menu responded", source: "Serilog.AspNetCore.RequestLoggingMiddleware"),
            Line("2026-11-17T10:01:00.0000000Z", "Error", "handler failed", source: "Sugarista.Application.Orders", exception: "NullReferenceException"),
            Line("2026-11-17T10:02:00.0000000Z", "Information", "EF query", source: "Microsoft.EntityFrameworkCore.Database.Command"),
            Line("2026-11-17T10:03:00.0000000Z", "Information", "no source"));

        var result = _store.ReadEntries("sugarista-20261117.log");

        Assert.Equal(4, result.TotalMatching);
        // Ordinal-sorted category list.
        Assert.Equal(
            ["اپلیکیشن", "درخواست HTTP", "عمومی", "فریم‌ورک"],
            result.Categories);

        // Newest first page order.
        Assert.Equal("no source", result.Entries[0].Message);
        Assert.Equal("Information", result.Entries[0].Level); // missing @l defaults

        var httpEntry = result.Entries.Single(entry => entry.Category == "درخواست HTTP");
        Assert.Equal("GET /api/menu responded", httpEntry.Message);
    }

    [Fact]
    public void ReadEntries_FiltersByLevelCategoryAndSearch()
    {
        WriteLogFile(
            _directory,
            "sugarista-20261117.log",
            Line("2026-11-17T10:00:00.0000000Z", "Information", "GET /health responded", source: "Serilog.AspNetCore.RequestLoggingMiddleware"),
            Line("2026-11-17T10:01:00.0000000Z", "Error", "espresso order failed", source: "Sugarista.Application.Pos"),
            Line("2026-11-17T10:02:00.0000000Z", "Warning", "slow request", source: "Serilog.AspNetCore.RequestLoggingMiddleware"));

        var errorsOnly = _store.ReadEntries("sugarista-20261117.log", level: "Error");
        Assert.Equal(1, errorsOnly.TotalMatching);
        Assert.Equal("espresso order failed", errorsOnly.Entries[0].Message);

        var httpOnly = _store.ReadEntries("sugarista-20261117.log", category: "درخواست HTTP");
        Assert.Equal(2, httpOnly.TotalMatching);

        var searchHit = _store.ReadEntries("sugarista-20261117.log", search: "espresso");
        Assert.Equal(1, searchHit.TotalMatching);

        var allFilters = _store.ReadEntries(
            "sugarista-20261117.log",
            level: "Error",
            category: "اپلیکیشن",
            search: "espresso");
        Assert.Equal(1, allFilters.TotalMatching);
    }

    [Fact]
    public void ReadEntries_PaginatesNewestFirst()
    {
        var lines = Enumerable.Range(1, 25)
            .Select(index => Line(
                $"2026-11-17T10:00:{index:00}.0000000Z",
                "Information",
                $"event {index:00}"))
            .ToArray();
        WriteLogFile(_directory, "sugarista-20261117.log", lines);

        var page1 = _store.ReadEntries("sugarista-20261117.log", page: 1, pageSize: 10);
        Assert.Equal(25, page1.TotalMatching);
        Assert.Equal("event 25", page1.Entries[0].Message);
        Assert.Equal("event 16", page1.Entries[9].Message);

        // 25 entries / 10 per page → page 3 holds the oldest 5.
        var page3 = _store.ReadEntries("sugarista-20261117.log", page: 3, pageSize: 10);
        Assert.Equal(5, page3.Entries.Count);
        Assert.Equal("event 05", page3.Entries[0].Message);
        Assert.Equal("event 01", page3.Entries[4].Message);

        // Beyond the last page → empty, never an error.
        var page4 = _store.ReadEntries("sugarista-20261117.log", page: 4, pageSize: 10);
        Assert.Empty(page4.Entries);
    }

    [Fact]
    public void ReadEntries_UnknownFile_ThrowsFileNotFound()
    {
        Assert.Throws<FileNotFoundException>(() => _store.ReadEntries("sugarista-19990101.log"));
    }

    [Fact]
    public void ReadEntries_ParsesExtraProperties()
    {
        WriteLogFile(
            _directory,
            "sugarista-20261117.log",
            Line(
                "2026-11-17T10:00:00.0000000Z",
                "Information",
                "HTTP GET responded",
                source: "Serilog.AspNetCore.RequestLoggingMiddleware",
                extra: ("StatusCode", "200")));

        var entry = _store.ReadEntries("sugarista-20261117.log").Entries.Single();

        Assert.Equal("200", entry.Properties["StatusCode"]);
    }

    [Fact]
    public void ReadEntries_RendersTemplateMessageWithProperties()
    {
        WriteLogFile(
            _directory,
            "sugarista-20261117.log",
            Line(
                "2026-11-17T10:00:00.0000000Z",
                "Information",
                "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0} ms",
                source: "Serilog.AspNetCore.RequestLoggingMiddleware",
                extra: ("StatusCode", "200")));

        // The helper adds properties as strings; emulate the real shape.
        var entry = _store.ReadEntries("sugarista-20261117.log").Entries.Single();
        Assert.Contains("200", entry.Message, StringComparison.Ordinal);
        Assert.Contains("ms", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DeleteFile_RemovesFileAndStopsListingIt()
    {
        WriteLogFile(_directory, "sugarista-20261116.log", Line("2026-11-16T10:00:00.0000000Z", "Information", "x"));
        WriteLogFile(_directory, "sugarista-20261117.log", Line("2026-11-17T10:00:00.0000000Z", "Information", "y"));

        Assert.True(_store.DeleteFile("sugarista-20261116.log"));
        Assert.False(File.Exists(Path.Combine(_directory, "sugarista-20261116.log")));
        Assert.Single(_store.ListFiles());

        // Deleting again (already gone) reports failure.
        Assert.False(_store.DeleteFile("sugarista-20261116.log"));
    }

    [Fact]
    public void DeleteFile_RejectsPathTraversalAndForeignNames()
    {
        Assert.False(_store.DeleteFile("../../etc/passwd"));
        Assert.False(_store.DeleteFile("audit-20261116.log"));
        Assert.False(_store.DeleteFile("sugarista-not-a-date.log"));

        // Nothing outside the directory was touched.
        Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(_directory)!, "passwd", SearchOption.AllDirectories));
    }

    [Fact]
    public void DeleteAllExcept_KeepsOnlyTheGivenFile()
    {
        WriteLogFile(_directory, "sugarista-20261113.log", Line("2026-11-13T10:00:00.0000000Z", "Information", "a"));
        WriteLogFile(_directory, "sugarista-20261115.log", Line("2026-11-15T10:00:00.0000000Z", "Information", "b"));
        WriteLogFile(_directory, "sugarista-20261117.log", Line("2026-11-17T10:00:00.0000000Z", "Information", "keep"));
        File.WriteAllText(Path.Combine(_directory, "audit-20261115.log"), "{}");

        var removed = _store.DeleteAllExcept("sugarista-20261117.log");

        Assert.Equal(2, removed);
        var remaining = Directory.GetFiles(_directory).Select(Path.GetFileName).ToList();
        Assert.Contains("sugarista-20261117.log", remaining);
        Assert.Contains("audit-20261115.log", remaining); // audit files are never touched
        Assert.Equal(2, remaining.Count);
    }
}
