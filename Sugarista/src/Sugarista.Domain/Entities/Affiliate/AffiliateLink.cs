using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public class AffiliateLink : BaseAuditableEntity
{
    private AffiliateLink()
    {
    }

    public AffiliateLink(
        Guid affiliateId,
        string slug,
        string targetUrl,
        string? subId = null)
    {
        if (affiliateId == Guid.Empty)
        {
            throw new DomainException("شناسه بازاریاب برای لینک الزامی است.");
        }

        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new DomainException("اسلاگ لینک بازاریاب الزامی است.");
        }

        if (string.IsNullOrWhiteSpace(targetUrl))
        {
            throw new DomainException("آدرس مقصد لینک الزامی است.");
        }

        AffiliateId = affiliateId;
        Slug = slug.Trim().ToLowerInvariant();
        SubId = string.IsNullOrWhiteSpace(subId) ? null : subId.Trim().ToLowerInvariant();
        TargetUrl = targetUrl.Trim();
        ClickCount = 0;
    }

    public Guid AffiliateId { get; private set; }

    public Affiliate Affiliate { get; private set; } = null!;

    public string Slug { get; private set; } = string.Empty;

    public string? SubId { get; private set; }

    public string TargetUrl { get; private set; } = string.Empty;

    public long ClickCount { get; private set; }

    public ICollection<ReferralClick> Clicks { get; private set; } = [];

    public void IncrementClickCount() => ClickCount++;

    public void UpdateTarget(string targetUrl, string? subId = null)
    {
        if (string.IsNullOrWhiteSpace(targetUrl))
        {
            throw new DomainException("آدرس مقصد لینک الزامی است.");
        }

        TargetUrl = targetUrl.Trim();
        SubId = string.IsNullOrWhiteSpace(subId) ? null : subId.Trim().ToLowerInvariant();
    }
}
