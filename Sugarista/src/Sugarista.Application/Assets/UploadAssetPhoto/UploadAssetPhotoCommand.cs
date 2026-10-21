using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Assets.Dto;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Assets.UploadAssetPhoto;

/// <summary>Uploads one photo for an asset (max 5 photos per asset).</summary>
public record UploadAssetPhotoCommand(
    Guid AssetId,
    StoredFileUpload File,
    string? Caption) : IRequest<CafeAssetPhotoDto>;

public class UploadAssetPhotoCommandHandler : IRequestHandler<UploadAssetPhotoCommand, CafeAssetPhotoDto>
{
    private const int MaxPhotosPerAsset = 5;

    private readonly IApplicationDbContext _dbContext;
    private readonly IFileStorageService _fileStorage;

    public UploadAssetPhotoCommandHandler(IApplicationDbContext dbContext, IFileStorageService fileStorage)
    {
        _dbContext = dbContext;
        _fileStorage = fileStorage;
    }

    public async Task<CafeAssetPhotoDto> Handle(
        UploadAssetPhotoCommand request,
        CancellationToken cancellationToken)
    {
        var asset = await _dbContext.CafeAssets
            .Include(item => item.Photos)
            .FirstOrDefaultAsync(item => item.Id == request.AssetId, cancellationToken)
            ?? throw new KeyNotFoundException("Asset was not found.");

        if (asset.Photos.Count >= MaxPhotosPerAsset)
        {
            throw new Sugarista.Domain.Exceptions.DomainException($"حداکثر {MaxPhotosPerAsset} عکس برای هر قلم قابل ثبت است.");
        }

        var url = await _fileStorage.SaveAssetPhotoAsync(request.File, cancellationToken);
        var isFirstPhoto = asset.Photos.Count == 0;

        var photo = new CafeAssetPhoto(asset.Id, url, request.Caption, isPrimary: isFirstPhoto);
        _dbContext.CafeAssetPhotos.Add(photo);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return CafeAssetMapper.ToDto(photo);
    }
}
