using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public enum BlacklistKind
{
    Phone = 0,
    IpAddress = 1,
}

/// <summary>
/// A phone number or IP address that may not use discounts. Entries are
/// soft-deactivated (kept for the audit trail) rather than deleted.
/// </summary>
public class BlacklistEntry : BaseAuditableEntity
{
    private BlacklistEntry()
    {
    }

    public BlacklistEntry(BlacklistKind kind, string value, string? reason = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("A blacklist entry needs a value.");
        }

        Kind = kind;
        Value = value.Trim().ToLowerInvariant();
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        IsActive = true;
    }

    public BlacklistKind Kind { get; private set; }

    public string Value { get; private set; } = string.Empty;

    public string? Reason { get; private set; }

    public bool IsActive { get; private set; }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    public void UpdateReason(string? reason) =>
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
}
