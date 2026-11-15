namespace Sugarista.Domain.Entities;

/// <summary>Sms provider implementations the system knows how to drive.</summary>
public enum SmsProviderType
{
    /// <summary>Logs instead of sending — development/testing.</summary>
    Logging = 0,

    Kavenegar = 1,

    SmsIr = 2,

    /// <summary>payamak-panel.com REST API (username/password auth).</summary>
    Melipayamak = 3,

    /// <summary>api.ghasedak.me REST v2 (apikey header).</summary>
    Ghasedak = 4,

    /// <summary>api.ippanel.com REST v1 (API key header).</summary>
    IpPanel = 5
}

/// <summary>Message categories. Marketing messages require recipient consent.</summary>
public enum SmsMessageKind
{
    Service = 0,
    Marketing = 1,
    Otp = 2
}

/// <summary>How the message entered the system.</summary>
public enum SmsSendType
{
    Single = 0,
    Bulk = 1,
    System = 2
}

/// <summary>
/// Standard send lifecycle. Accepted/Sent intentionally distinct from
/// Delivered: an operator handset confirming delivery is a separate fact.
/// </summary>
public enum SmsSendStatus
{
    Pending = 0,
    Queued = 1,
    Processing = 2,
    Accepted = 3,
    Sent = 4,
    Delivered = 5,
    Failed = 6,
    Rejected = 7,
    Unknown = 8
}

/// <summary>Campaign lifecycle for bulk sends.</summary>
public enum SmsCampaignStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Cancelled = 3,
    Failed = 4
}
