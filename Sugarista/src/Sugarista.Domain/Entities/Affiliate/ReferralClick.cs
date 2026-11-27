using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public class ReferralClick : BaseAuditableEntity
{
    private ReferralClick()
    {
    }

    public ReferralClick(
        Guid linkId,
        Guid trackingToken,
        DateTimeOffset expiresAt,
        string? ipAddress = null,
        string? userAgent = null)
    {
        if (linkId == Guid.Empty)
        {
            throw new DomainException("شناسه لینک ارجاع الزامی است.");
        }

        if (trackingToken == Guid.Empty)
        {
            throw new DomainException("توکن رهگیری کلیک نمی‌تواند خالی باشد.");
        }

        LinkId = linkId;
        TrackingToken = trackingToken;
        IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? null : ipAddress.Trim();
        UserAgent = string.IsNullOrWhiteSpace(userAgent) ? null : userAgent.Trim();
        ExpiresAt = expiresAt;
    }

    public Guid LinkId { get; private set; }

    public AffiliateLink Link { get; private set; } = null!;

    public Guid TrackingToken { get; private set; }

    public string? IpAddress { get; private set; }

    public string? UserAgent { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;
}
