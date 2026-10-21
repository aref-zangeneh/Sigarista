using Sugarista.Application.Common.Models;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Assets.Dto;

public record CafeAssetPhotoDto(
    Guid Id,
    string ImageUrl,
    string? Caption,
    bool IsPrimary);

public record CafeAssetDto(
    Guid Id,
    string PersianName,
    string? EnglishName,
    string Category,
    string? SerialNumber,
    string Location,
    DateOnly? PurchaseDate,
    decimal UnitPrice,
    int Quantity,
    decimal TotalValue,
    string? SupplierName,
    string? Note,
    bool IsActive,
    string? DisposalNote,
    DateTimeOffset? DisposedAt,
    IReadOnlyList<CafeAssetPhotoDto> Photos);

public record CafeAssetSummaryDto(
    int ActiveItems,
    int RetiredItems,
    int TotalUnits,
    decimal ActiveValue,
    decimal RetiredValue);

public static class CafeAssetMapper
{
    public static CafeAssetPhotoDto ToDto(CafeAssetPhoto photo) => new(
        photo.Id,
        photo.ImageUrl,
        photo.Caption,
        photo.IsPrimary);

    public static CafeAssetDto ToDto(CafeAsset asset) => new(
        asset.Id,
        asset.PersianName,
        asset.EnglishName,
        asset.Category,
        asset.SerialNumber,
        asset.Location,
        asset.PurchaseDate,
        asset.UnitPrice,
        asset.Quantity,
        asset.TotalValue,
        asset.SupplierName,
        asset.Note,
        asset.IsActive,
        asset.DisposalNote,
        asset.DisposedAt,
        asset.Photos
            .Select(ToDto)
            .OrderByDescending(photo => photo.IsPrimary)
            .ToList());
}
