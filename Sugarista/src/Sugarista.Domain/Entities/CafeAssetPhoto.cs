using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// One photo of a café asset. The image file is uploaded through the shared
/// file-storage pipeline; only the URL lives on the asset row.
/// </summary>
public class CafeAssetPhoto : BaseEntity
{
    private CafeAssetPhoto()
    {
    }

    public CafeAssetPhoto(Guid assetId, string imageUrl, string? caption, bool isPrimary)
    {
        if (assetId == Guid.Empty)
        {
            throw new DomainException("Asset is required.");
        }

        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            throw new DomainException("آدرس تصویر الزامی است.");
        }

        AssetId = assetId;
        ImageUrl = imageUrl.Trim();
        Caption = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim();
        IsPrimary = isPrimary;
    }

    public Guid AssetId { get; private set; }

    public CafeAsset CafeAsset { get; private set; } = null!;

    public string ImageUrl { get; private set; } = string.Empty;

    public string? Caption { get; private set; }

    public bool IsPrimary { get; private set; }

    public void MakePrimary()
    {
        IsPrimary = true;
    }

    public void ClearPrimary()
    {
        IsPrimary = false;
    }

    public void SetCaption(string? caption)
    {
        Caption = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim();
    }
}
