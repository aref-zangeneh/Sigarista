using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Sugarista.Api.Monitoring;

/// <summary>
/// Ultra-lightweight, lock-free / bounded-memory telemetry and security collector.
/// Keeps ZERO database writes on the HTTP hot path so site performance is never impacted.
/// </summary>
public sealed class SystemTelemetryCollector
{
    private const int MaxRecentRequests = 180;
    private const int MaxRecentErrors = 50;
    private const int MaxSlowRequests = 40;
    private const int MaxSecurityEvents = 80;
    private const int MaxHistoryPoints = 40;
    private const int MaxTrackedEndpoints = 200;
    private const int MaxTrackedIps = 200;
    private const double SlowRequestThresholdMs = 500.0;

    private static readonly Regex GuidSegmentRegex = new(
        @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
        RegexOptions.Compiled);

    private static readonly Regex NumericSegmentRegex = new(@"/\d+(?=/|$)", RegexOptions.Compiled);

    private readonly DateTimeOffset _startedAtUtc = DateTimeOffset.UtcNow;
    private readonly Process _currentProcess = Process.GetCurrentProcess();
    private readonly object _cpuLock = new();
    private DateTime _lastCpuSampleUtc = DateTime.UtcNow;
    private TimeSpan _lastTotalProcessorTime;
    private double _lastCpuPercent;

    private long _totalRequests;
    private long _total2xx;
    private long _total4xx;
    private long _total5xx;
    private long _totalExceptions;
    private long _inFlightRequests;
    private long _totalLatencyMsBits;

    private readonly ConcurrentQueue<RequestTraceEntry> _recentRequests = new();
    private readonly ConcurrentQueue<ErrorTraceEntry> _recentErrors = new();
    private readonly ConcurrentQueue<RequestTraceEntry> _slowRequests = new();
    private readonly ConcurrentQueue<SecurityEventEntry> _securityEvents = new();
    private readonly ConcurrentQueue<ResourceSamplePoint> _resourceHistory = new();

    private readonly ConcurrentDictionary<string, EndpointMetricBucket> _endpoints = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, IpThreatBucket> _suspiciousIps = new(StringComparer.OrdinalIgnoreCase);

    public SystemTelemetryCollector()
    {
        try
        {
            _lastTotalProcessorTime = _currentProcess.TotalProcessorTime;
        }
        catch
        {
            _lastTotalProcessorTime = TimeSpan.Zero;
        }
    }

    public void BeginRequest() => Interlocked.Increment(ref _inFlightRequests);

    public void EndRequest() => Interlocked.Decrement(ref _inFlightRequests);

    public void RecordRequest(
        string method,
        string rawPath,
        string? queryString,
        int statusCode,
        double durationMs,
        string clientIp,
        string? exceptionMessage = null)
    {
        var now = DateTimeOffset.UtcNow;
        var normalizedPath = NormalizePath(rawPath);
        var endpointKey = $"{method} {normalizedPath}";

        Interlocked.Increment(ref _totalRequests);
        AddDoubleAtomically(ref _totalLatencyMsBits, durationMs);

        if (statusCode >= 500)
        {
            Interlocked.Increment(ref _total5xx);
        }
        else if (statusCode >= 400)
        {
            Interlocked.Increment(ref _total4xx);
        }
        else if (statusCode >= 200 && statusCode < 300)
        {
            Interlocked.Increment(ref _total2xx);
        }

        if (!string.IsNullOrWhiteSpace(exceptionMessage))
        {
            Interlocked.Increment(ref _totalExceptions);
        }

        var trace = new RequestTraceEntry(
            now,
            method,
            normalizedPath,
            statusCode,
            Math.Round(durationMs, 1),
            clientIp);

        EnqueueBounded(_recentRequests, trace, MaxRecentRequests);

        if (durationMs >= SlowRequestThresholdMs)
        {
            EnqueueBounded(_slowRequests, trace, MaxSlowRequests);
        }

        if (statusCode >= 500 || !string.IsNullOrWhiteSpace(exceptionMessage))
        {
            EnqueueBounded(
                _recentErrors,
                new ErrorTraceEntry(
                    now,
                    method,
                    normalizedPath,
                    statusCode,
                    Math.Round(durationMs, 1),
                    clientIp,
                    Truncate(exceptionMessage ?? $"HTTP {statusCode}", 260)),
                MaxRecentErrors);
        }

        if (_endpoints.Count < MaxTrackedEndpoints || _endpoints.ContainsKey(endpointKey))
        {
            var bucket = _endpoints.GetOrAdd(endpointKey, _ => new EndpointMetricBucket(method, normalizedPath));
            bucket.Record(statusCode, durationMs, now);
        }

        // Security inspection (O(1) substring checks — zero regex on hot path)
        InspectSecuritySignals(now, method, rawPath, queryString, statusCode, clientIp);
    }

    public void Reset()
    {
        Interlocked.Exchange(ref _totalRequests, 0);
        Interlocked.Exchange(ref _total2xx, 0);
        Interlocked.Exchange(ref _total4xx, 0);
        Interlocked.Exchange(ref _total5xx, 0);
        Interlocked.Exchange(ref _totalExceptions, 0);
        Interlocked.Exchange(ref _totalLatencyMsBits, 0);

        while (_recentRequests.TryDequeue(out _)) { }
        while (_recentErrors.TryDequeue(out _)) { }
        while (_slowRequests.TryDequeue(out _)) { }
        while (_securityEvents.TryDequeue(out _)) { }
        _endpoints.Clear();
        _suspiciousIps.Clear();
    }

    public MonitoringSnapshotDto CaptureSnapshot(DatabaseHealthDto dbHealth, SecurityConfigAuditDto securityAudit)
    {
        var now = DateTimeOffset.UtcNow;
        var cpuPercent = SampleCpuPercent();

        _currentProcess.Refresh();
        var workingSetMb = Math.Round(_currentProcess.WorkingSet64 / (1024.0 * 1024.0), 1);
        var managedHeapMb = Math.Round(GC.GetTotalMemory(false) / (1024.0 * 1024.0), 1);
        var gcInfo = GC.GetGCMemoryInfo();
        var totalSystemMemMb = gcInfo.TotalAvailableMemoryBytes > 0
            ? Math.Round(gcInfo.TotalAvailableMemoryBytes / (1024.0 * 1024.0), 0)
            : 0;
        var memUsagePct = totalSystemMemMb > 0
            ? Math.Round(Math.Min(100.0, workingSetMb / totalSystemMemMb * 100.0), 1)
            : 0;

        ThreadPool.GetAvailableThreads(out var availWorkers, out var availIo);
        ThreadPool.GetMaxThreads(out var maxWorkers, out var maxIo);

        var drives = CaptureDrives();

        var samplePoint = new ResourceSamplePoint(now, cpuPercent, workingSetMb, (int)Interlocked.Read(ref _inFlightRequests));
        EnqueueBounded(_resourceHistory, samplePoint, MaxHistoryPoints);

        var recent = _recentRequests.ToArray();
        var oneMinuteAgo = now.AddMinutes(-1);
        var lastMinuteRequests = recent.Where(r => r.Timestamp >= oneMinuteAgo).ToList();
        var rpm = lastMinuteRequests.Count;

        var durations = recent.Select(r => r.DurationMs).OrderBy(x => x).ToArray();
        var p95Ms = durations.Length > 0
            ? durations[(int)Math.Clamp(Math.Ceiling(durations.Length * 0.95) - 1, 0, durations.Length - 1)]
            : 0;

        var totalReq = Interlocked.Read(ref _totalRequests);
        var total5xx = Interlocked.Read(ref _total5xx);
        var total4xx = Interlocked.Read(ref _total4xx);
        var avgMs = totalReq > 0
            ? Math.Round(BitConverter.Int64BitsToDouble(Interlocked.Read(ref _totalLatencyMsBits)) / totalReq, 1)
            : 0;
        var errorRatePct = totalReq > 0 ? Math.Round((double)total5xx / totalReq * 100.0, 2) : 0;

        var endpointList = _endpoints.Values
            .Select(b => b.ToDto())
            .OrderByDescending(e => e.Error5xxCount)
            .ThenByDescending(e => e.AvgDurationMs)
            .ThenByDescending(e => e.TotalRequests)
            .Take(40)
            .ToList();

        var securityEventsList = _securityEvents.Reverse().Take(50).ToList();
        var suspiciousIpsList = _suspiciousIps.Values
            .Select(b => b.ToDto())
            .OrderByDescending(i => i.TotalThreatScore)
            .ThenByDescending(i => i.LastSeenAt)
            .Take(20)
            .ToList();

        var highSeveritySecCount = securityEventsList.Count(e => e.Severity == "High");
        var securityScore = ComputeSecurityScore(highSeveritySecCount, securityEventsList.Count, securityAudit);

        var overallHealth = DetermineOverallHealth(cpuPercent, memUsagePct, drives, dbHealth, errorRatePct, highSeveritySecCount);

        return new MonitoringSnapshotDto(
            CapturedAtUtc: now,
            OverallHealth: overallHealth,
            Server: new ServerMetricsDto(
                MachineName: Environment.MachineName,
                OsDescription: RuntimeInformation.OSDescription,
                DotNetVersion: RuntimeInformation.FrameworkDescription,
                ProcessorCount: Environment.ProcessorCount,
                UptimeSeconds: (long)(now - _startedAtUtc).TotalSeconds,
                CpuUsagePercent: cpuPercent,
                WorkingSetMb: workingSetMb,
                ManagedHeapMb: managedHeapMb,
                TotalSystemMemoryMb: totalSystemMemMb,
                MemoryUsagePercent: memUsagePct,
                GcGen0Collections: GC.CollectionCount(0),
                GcGen1Collections: GC.CollectionCount(1),
                GcGen2Collections: GC.CollectionCount(2),
                ActiveWorkerThreads: Math.Max(0, maxWorkers - availWorkers),
                MaxWorkerThreads: maxWorkers,
                ActiveIoThreads: Math.Max(0, maxIo - availIo),
                PendingThreadPoolWorkItems: ThreadPool.PendingWorkItemCount,
                Drives: drives,
                History: _resourceHistory.ToArray()),
            Database: dbHealth,
            Application: new ApplicationMetricsDto(
                TotalRequests: totalReq,
                Total2xx: Interlocked.Read(ref _total2xx),
                Total4xx: total4xx,
                Total5xx: total5xx,
                TotalExceptions: Interlocked.Read(ref _totalExceptions),
                InFlightRequests: (int)Interlocked.Read(ref _inFlightRequests),
                RequestsPerMinute: rpm,
                AvgLatencyMs: avgMs,
                P95LatencyMs: Math.Round(p95Ms, 1),
                ErrorRatePercent: errorRatePct,
                Endpoints: endpointList,
                RecentErrors: _recentErrors.Reverse().Take(25).ToList(),
                SlowRequests: _slowRequests.Reverse().Take(25).ToList()),
            Security: new SecurityMetricsDto(
                SecurityScore: securityScore,
                TotalSecurityEvents: securityEventsList.Count,
                HighSeverityEvents: highSeveritySecCount,
                RateLimitedCount: securityEventsList.Count(e => e.EventType == "RateLimited"),
                AuthFailureCount: securityEventsList.Count(e => e.EventType is "AuthFailure" or "Forbidden"),
                ProbeAttemptCount: securityEventsList.Count(e => e.EventType is "SqlInjectionProbe" or "XssProbe" or "PathTraversalProbe" or "ScannerProbe"),
                Audit: securityAudit,
                RecentEvents: securityEventsList,
                SuspiciousIps: suspiciousIpsList));
    }

    private double SampleCpuPercent()
    {
        lock (_cpuLock)
        {
            try
            {
                _currentProcess.Refresh();
                var now = DateTime.UtcNow;
                var currentCpu = _currentProcess.TotalProcessorTime;
                var elapsedMs = (now - _lastCpuSampleUtc).TotalMilliseconds;

                if (elapsedMs >= 400)
                {
                    var cpuUsedMs = (currentCpu - _lastTotalProcessorTime).TotalMilliseconds;
                    var pct = cpuUsedMs / (Environment.ProcessorCount * elapsedMs) * 100.0;
                    _lastCpuPercent = Math.Round(Math.Clamp(pct, 0, 100), 1);
                    _lastCpuSampleUtc = now;
                    _lastTotalProcessorTime = currentCpu;
                }

                return _lastCpuPercent;
            }
            catch
            {
                return _lastCpuPercent;
            }
        }
    }

    private static IReadOnlyList<DriveMetricDto> CaptureDrives()
    {
        var list = new List<DriveMetricDto>();
        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady || drive.TotalSize <= 0) continue;
                // Skip tiny pseudo/virtual filesystems on Linux/macOS containers
                if (drive.TotalSize < 100L * 1024 * 1024) continue;
                if (drive.Name.StartsWith("/System/Volumes/", StringComparison.Ordinal) &&
                    !drive.Name.Equals("/System/Volumes/Data", StringComparison.Ordinal))
                {
                    continue;
                }

                var totalGb = Math.Round(drive.TotalSize / (1024.0 * 1024.0 * 1024.0), 1);
                var freeGb = Math.Round(drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0), 1);
                var usedGb = Math.Round(Math.Max(0, totalGb - freeGb), 1);
                var usedPct = totalGb > 0 ? Math.Round(usedGb / totalGb * 100.0, 1) : 0;

                list.Add(new DriveMetricDto(
                    drive.Name,
                    drive.DriveFormat,
                    totalGb,
                    usedGb,
                    freeGb,
                    usedPct));
            }
        }
        catch
        {
            // Ignore drive enumeration failures on restricted sandboxes
        }

        return list.Take(6).ToList();
    }

    private void InspectSecuritySignals(
        DateTimeOffset now,
        string method,
        string rawPath,
        string? queryString,
        int statusCode,
        string clientIp)
    {
        var combined = string.IsNullOrEmpty(queryString) ? rawPath : $"{rawPath}{queryString}";

        string? detectedType = null;
        string severity = "Low";
        string summary = string.Empty;

        if (ContainsAny(combined, "../", "..\\", "%2e%2e", "/etc/passwd"))
        {
            detectedType = "PathTraversalProbe";
            severity = "High";
            summary = "تلاش مشکوک برای دسترسی خارج از مسیر (Path Traversal)";
        }
        else if (ContainsAny(combined, "union select", "or 1=1", "drop table", "xp_cmdshell", "'--", "%27%20or%20"))
        {
            detectedType = "SqlInjectionProbe";
            severity = "High";
            summary = "الگوی مشکوک تزریق SQL در درخواست";
        }
        else if (ContainsAny(combined, "<script", "javascript:", "onerror=", "onload="))
        {
            detectedType = "XssProbe";
            severity = "High";
            summary = "الگوی مشکوک تزریق اسکریپت (XSS)";
        }
        else if (ContainsAny(rawPath, "/.env", "/wp-admin", "/wp-login", "/phpmyadmin", "/.git", "/xmlrpc.php", "/actuator"))
        {
            detectedType = "ScannerProbe";
            severity = "Medium";
            summary = "اسکن خودکار مسیرهای حساس (Bot / Scanner)";
        }
        else if (statusCode == 429)
        {
            detectedType = "RateLimited";
            severity = "Medium";
            summary = "عبور از سقف مجاز درخواست (Rate Limit 429)";
        }
        else if (statusCode == 401 && rawPath.Contains("/auth", StringComparison.OrdinalIgnoreCase))
        {
            detectedType = "AuthFailure";
            severity = "Medium";
            summary = "تلاش ناموفق ورود / احراز هویت (401)";
        }
        else if (statusCode == 403)
        {
            detectedType = "Forbidden";
            severity = "Low";
            summary = "تلاش دسترسی غیرمجاز به بخش محافظت‌شده (403)";
        }

        if (detectedType is null) return;

        EnqueueBounded(
            _securityEvents,
            new SecurityEventEntry(
                now,
                detectedType,
                severity,
                clientIp,
                method,
                Truncate(rawPath, 160),
                statusCode,
                summary),
            MaxSecurityEvents);

        if (_suspiciousIps.Count < MaxTrackedIps || _suspiciousIps.ContainsKey(clientIp))
        {
            var bucket = _suspiciousIps.GetOrAdd(clientIp, ip => new IpThreatBucket(ip));
            bucket.Record(detectedType, severity, now);
        }
    }

    private static bool ContainsAny(string haystack, params string[] needles)
    {
        foreach (var needle in needles)
        {
            if (haystack.Contains(needle, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static string NormalizePath(string rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath)) return "/";
        var step1 = GuidSegmentRegex.Replace(rawPath, "{id}");
        var step2 = NumericSegmentRegex.Replace(step1, "/{n}");
        return step2.Length > 120 ? step2[..120] : step2;
    }

    private static int ComputeSecurityScore(int highEvents, int totalEvents, SecurityConfigAuditDto audit)
    {
        var score = 100;
        if (!audit.RateLimitingEnabled) score -= 20;
        if (!audit.SecurityHeadersEnabled) score -= 15;
        if (!audit.HttpsActive) score -= 10;
        score -= Math.Min(30, highEvents * 6);
        score -= Math.Min(15, Math.Max(0, totalEvents - highEvents));
        return Math.Clamp(score, 0, 100);
    }

    private static string DetermineOverallHealth(
        double cpu,
        double mem,
        IReadOnlyList<DriveMetricDto> drives,
        DatabaseHealthDto db,
        double errorRatePct,
        int highSecEvents)
    {
        if (!db.IsHealthy || cpu >= 90 || mem >= 92 || drives.Any(d => d.UsedPercent >= 95) || errorRatePct >= 10)
        {
            return "Critical";
        }

        if (cpu >= 70 || mem >= 80 || drives.Any(d => d.UsedPercent >= 85) || errorRatePct >= 3 || highSecEvents >= 5 || db.LatencyMs >= 250)
        {
            return "Warning";
        }

        return "Healthy";
    }

    private static void EnqueueBounded<T>(ConcurrentQueue<T> queue, T item, int max)
    {
        queue.Enqueue(item);
        while (queue.Count > max && queue.TryDequeue(out _)) { }
    }

    private static void AddDoubleAtomically(ref long locationBits, double value)
    {
        while (true)
        {
            var initial = Volatile.Read(ref locationBits);
            var updated = BitConverter.DoubleToInt64Bits(BitConverter.Int64BitsToDouble(initial) + value);
            if (Interlocked.CompareExchange(ref locationBits, updated, initial) == initial)
            {
                return;
            }
        }
    }

    private static string Truncate(string value, int maxLen)
        => value.Length > maxLen ? value[..maxLen] : value;

    private sealed class EndpointMetricBucket(string method, string path)
    {
        private long _count;
        private long _err4xx;
        private long _err5xx;
        private long _totalMsBits;
        private long _maxMsBits;
        private long _lastSeenTicks;

        public void Record(int statusCode, double durationMs, DateTimeOffset now)
        {
            Interlocked.Increment(ref _count);
            if (statusCode >= 500) Interlocked.Increment(ref _err5xx);
            else if (statusCode >= 400) Interlocked.Increment(ref _err4xx);

            AddDoubleAtomically(ref _totalMsBits, durationMs);
            UpdateMaxAtomically(ref _maxMsBits, durationMs);
            Interlocked.Exchange(ref _lastSeenTicks, now.UtcTicks);
        }

        public EndpointMetricDto ToDto()
        {
            var count = Interlocked.Read(ref _count);
            var err4xx = Interlocked.Read(ref _err4xx);
            var err5xx = Interlocked.Read(ref _err5xx);
            var avgMs = count > 0 ? Math.Round(BitConverter.Int64BitsToDouble(Volatile.Read(ref _totalMsBits)) / count, 1) : 0;
            var maxMs = Math.Round(BitConverter.Int64BitsToDouble(Volatile.Read(ref _maxMsBits)), 1);
            var health = err5xx > 0 ? "Error" : avgMs >= SlowRequestThresholdMs ? "Slow" : "Healthy";

            return new EndpointMetricDto(
                method,
                path,
                count,
                err4xx,
                err5xx,
                avgMs,
                maxMs,
                health,
                new DateTimeOffset(Volatile.Read(ref _lastSeenTicks), TimeSpan.Zero));
        }

        private static void UpdateMaxAtomically(ref long locationBits, double candidate)
        {
            while (true)
            {
                var initial = Volatile.Read(ref locationBits);
                if (BitConverter.Int64BitsToDouble(initial) >= candidate) return;
                var updated = BitConverter.DoubleToInt64Bits(candidate);
                if (Interlocked.CompareExchange(ref locationBits, updated, initial) == initial) return;
            }
        }
    }

    private sealed class IpThreatBucket(string ip)
    {
        private long _authFailures;
        private long _rateLimits;
        private long _probes;
        private long _threatScore;
        private long _lastSeenTicks;

        public void Record(string eventType, string severity, DateTimeOffset now)
        {
            if (eventType is "AuthFailure" or "Forbidden") Interlocked.Increment(ref _authFailures);
            else if (eventType == "RateLimited") Interlocked.Increment(ref _rateLimits);
            else Interlocked.Increment(ref _probes);

            var weight = severity == "High" ? 10 : severity == "Medium" ? 4 : 1;
            Interlocked.Add(ref _threatScore, weight);
            Interlocked.Exchange(ref _lastSeenTicks, now.UtcTicks);
        }

        public SuspiciousIpDto ToDto() => new(
            ip,
            Interlocked.Read(ref _authFailures),
            Interlocked.Read(ref _rateLimits),
            Interlocked.Read(ref _probes),
            Interlocked.Read(ref _threatScore),
            new DateTimeOffset(Volatile.Read(ref _lastSeenTicks), TimeSpan.Zero));
    }
}

public record MonitoringSnapshotDto(
    DateTimeOffset CapturedAtUtc,
    string OverallHealth,
    ServerMetricsDto Server,
    DatabaseHealthDto Database,
    ApplicationMetricsDto Application,
    SecurityMetricsDto Security);

public record ServerMetricsDto(
    string MachineName,
    string OsDescription,
    string DotNetVersion,
    int ProcessorCount,
    long UptimeSeconds,
    double CpuUsagePercent,
    double WorkingSetMb,
    double ManagedHeapMb,
    double TotalSystemMemoryMb,
    double MemoryUsagePercent,
    int GcGen0Collections,
    int GcGen1Collections,
    int GcGen2Collections,
    int ActiveWorkerThreads,
    int MaxWorkerThreads,
    int ActiveIoThreads,
    long PendingThreadPoolWorkItems,
    IReadOnlyList<DriveMetricDto> Drives,
    IReadOnlyList<ResourceSamplePoint> History);

public record DriveMetricDto(
    string Name,
    string Format,
    double TotalGb,
    double UsedGb,
    double FreeGb,
    double UsedPercent);

public record ResourceSamplePoint(
    DateTimeOffset Timestamp,
    double CpuPercent,
    double MemoryMb,
    int InFlight);

public record DatabaseHealthDto(
    bool IsHealthy,
    double LatencyMs,
    string StatusMessage);

public record ApplicationMetricsDto(
    long TotalRequests,
    long Total2xx,
    long Total4xx,
    long Total5xx,
    long TotalExceptions,
    int InFlightRequests,
    int RequestsPerMinute,
    double AvgLatencyMs,
    double P95LatencyMs,
    double ErrorRatePercent,
    IReadOnlyList<EndpointMetricDto> Endpoints,
    IReadOnlyList<ErrorTraceEntry> RecentErrors,
    IReadOnlyList<RequestTraceEntry> SlowRequests);

public record EndpointMetricDto(
    string Method,
    string Path,
    long TotalRequests,
    long Error4xxCount,
    long Error5xxCount,
    double AvgDurationMs,
    double MaxDurationMs,
    string Health,
    DateTimeOffset LastSeenAt);

public record RequestTraceEntry(
    DateTimeOffset Timestamp,
    string Method,
    string Path,
    int StatusCode,
    double DurationMs,
    string ClientIp);

public record ErrorTraceEntry(
    DateTimeOffset Timestamp,
    string Method,
    string Path,
    int StatusCode,
    double DurationMs,
    string ClientIp,
    string Message);

public record SecurityMetricsDto(
    int SecurityScore,
    int TotalSecurityEvents,
    int HighSeverityEvents,
    int RateLimitedCount,
    int AuthFailureCount,
    int ProbeAttemptCount,
    SecurityConfigAuditDto Audit,
    IReadOnlyList<SecurityEventEntry> RecentEvents,
    IReadOnlyList<SuspiciousIpDto> SuspiciousIps);

public record SecurityConfigAuditDto(
    bool HttpsActive,
    bool RateLimitingEnabled,
    bool SecurityHeadersEnabled,
    int ActiveAdminUsersCount);

public record SecurityEventEntry(
    DateTimeOffset Timestamp,
    string EventType,
    string Severity,
    string ClientIp,
    string Method,
    string Path,
    int StatusCode,
    string Summary);

public record SuspiciousIpDto(
    string ClientIp,
    long AuthFailures,
    long RateLimits,
    long ProbeAttempts,
    long TotalThreatScore,
    DateTimeOffset LastSeenAt);
