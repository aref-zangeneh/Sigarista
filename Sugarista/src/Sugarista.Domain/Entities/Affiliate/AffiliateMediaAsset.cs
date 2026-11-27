using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public enum AffiliateMediaAssetType
{
    Image = 0,
    Video = 1,
    Logo = 2,
    CopyText = 3
}

/// <summary>
/// Promotional asset uploaded by admin for affiliates to download or copy
/// (photos, reels videos, brand logos, and suggested ad copy).
/// </summary>
public class AffiliateMediaAsset : BaseAuditableEntity
{
    private AffiliateMediaAsset()
    {
    }

    public AffiliateMediaAsset(
        string title,
        AffiliateMediaAssetType assetType,
        string? fileUrl = null,
        string? captionText = null,
        string? thumbnailUrl = null,
        int displayOrder = 0,
        bool isActive = true)
    {
        Update(title, assetType, fileUrl, captionText, thumbnailUrl, displayOrder, isActive);
    }

    public string Title { get; private set; } = string.Empty;

    public AffiliateMediaAssetType AssetType { get; private set; }

    public string? FileUrl { get; private set; }

    public string? CaptionText { get; private set; }

    public string? ThumbnailUrl { get; private set; }

    public int DisplayOrder { get; private set; }

    public bool IsActive { get; private set; } = true;

    public void Update(
        string title,
        AffiliateMediaAssetType assetType,
        string? fileUrl,
        string? captionText,
        string? thumbnailUrl,
        int displayOrder,
        bool isActive)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("عنوان فایل تبلیغاتی الزامی است.");
        }

        if (assetType == AffiliateMediaAssetType.CopyText && string.IsNullOrWhiteSpace(captionText))
        {
            throw new DomainException("متن تبلیغاتی برای این نوع محتوا الزامی است.");
        }

        if (assetType != AffiliateMediaAssetType.CopyText && string.IsNullOrWhiteSpace(fileUrl))
        {
            throw new DomainException("آدرس فایل برای عکس، ویدیو یا لوگو الزامی است.");
        }

        Title = title.Trim();
        AssetType = assetType;
        FileUrl = string.IsNullOrWhiteSpace(fileUrl) ? null : fileUrl.Trim();
        CaptionText = string.IsNullOrWhiteSpace(captionText) ? null : captionText.Trim();
        ThumbnailUrl = string.IsNullOrWhiteSpace(thumbnailUrl) ? null : thumbnailUrl.Trim();
        DisplayOrder = displayOrder;
        IsActive = isActive;
    }
}
