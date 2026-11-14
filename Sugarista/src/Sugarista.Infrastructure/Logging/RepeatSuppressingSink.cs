using System.Security.Cryptography;
using System.Text;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

namespace Sugarista.Infrastructure.Logging;

/// <summary>
/// Collapses identical consecutive events (same message template + level +
/// same property values) inside a short window: retry loops, health checks,
/// "database not ready yet" tickers and other cadence logs would otherwise
/// flood the files with byte-identical lines.
///
/// The first event of a series is emitted normally; suppressed events are
/// counted and, when the series ends (different event or window elapsed), a
/// single aggregate line is emitted stating how many times the previous
/// event repeated. State is one slot + one dictionary, so the memory
/// footprint is trivial and bounded.
/// </summary>
public sealed class RepeatSuppressingSink : ILogEventSink
{
    private readonly ILogEventSink _inner;
    private readonly TimeSpan _window;
    private readonly object _gate = new();

    private string? _lastKey;
    private LogEvent? _lastEvent;
    private DateTime _lastUtc;
    private int _repeatCount;

    public RepeatSuppressingSink(ILogEventSink inner, TimeSpan window)
    {
        _inner = inner;
        _window = window;
    }

    public void Emit(LogEvent logEvent)
    {
        string key;
        lock (_gate)
        {
            key = BuildKey(logEvent);
            var now = DateTime.UtcNow;

            if (key == _lastKey && (now - _lastUtc) < _window && _lastEvent is not null)
            {
                _repeatCount++;
                _lastUtc = now;
                return; // drop the repeat
            }

            // The series ended — first flush the aggregate of what we held back.
            var pendingEvent = _lastEvent;
            var pendingCount = _repeatCount;
            _lastKey = key;
            _lastEvent = logEvent;
            _lastUtc = now;
            _repeatCount = 0;

            EmitHeldBack(pendingEvent, pendingCount);
        }

        _inner.Emit(logEvent);
    }

    private void EmitHeldBack(LogEvent? pendingEvent, int pendingCount)
    {
        if (pendingEvent is null || pendingCount <= 0)
        {
            return;
        }

        var template = pendingEvent.MessageTemplate.Text;
        if (template.Length > 200)
        {
            template = template[..200] + "…";
        }

        _inner.Emit(new LogEvent(
            pendingEvent.Timestamp,
            LogEventLevel.Information,
            null,
            new MessageTemplateParser().Parse(
                "Repeated log suppressed ×{RepeatCount} — template: {SuppressedTemplate}"),
            new[]
            {
                new LogEventProperty("RepeatCount", new ScalarValue(pendingCount + 1)),
                new LogEventProperty("SuppressedTemplate", new ScalarValue(template))
            }));
    }

    private static string BuildKey(LogEvent logEvent)
    {
        // Template + level catches cadence logs; a cheap stable hash of the
        // rendered properties catches identical single-instance messages too
        // (e.g. "Order 42 saved" repeated), without storing whole events.
        Span<byte> hash = stackalloc byte[16];
        MD5.HashData(Encoding.UTF8.GetBytes(logEvent.RenderMessage()), hash);
        return $"{logEvent.Level}:{logEvent.MessageTemplate.Text}:{Convert.ToHexString(hash)}";
    }
}
