namespace Sugarista.Application.Common.Interfaces;

/// <summary>
/// Separate audit trail for security-relevant events (password rotations,
/// route toggles, log-level changes, …). Writes to its own dedicated file —
/// NOT through the bounded/dropping pipeline — so audit records are never
/// dropped when the regular log queue is full.
/// </summary>
public interface IAuditLog
{
    /// <summary>Records one audit event. Must never throw into the caller.</summary>
    void Write(string action, string? detail = null, object? data = null);
}
