using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.Assets.ManageAssetPhoto;

public record SetPrimaryAssetPhotoCommand(Guid PhotoId) : IRequest;

public class SetPrimaryAssetPhotoCommandHandler : IRequestHandler<SetPrimaryAssetPhotoCommand>
{
    private readonly IApplicationDbContext _dbContext;

    public SetPrimaryAssetPhotoCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(SetPrimaryAssetPhotoCommand request, CancellationToken cancellationToken)
    {
        var photo = await _dbContext.CafeAssetPhotos
            .Include(item => item.CafeAsset)
            .FirstOrDefaultAsync(item => item.Id == request.PhotoId, cancellationToken)
            ?? throw new KeyNotFoundException("Photo was not found.");

        var oldPrimary = await _dbContext.CafeAssetPhotos
            .Where(item => item.AssetId == photo.AssetId && item.Id != photo.Id && item.IsPrimary)
            .ToListAsync(cancellationToken);

        foreach (var other in oldPrimary)
        {
            other.ClearPrimary();
        }

        photo.MakePrimary();
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}

public record DeleteAssetPhotoCommand(Guid PhotoId) : IRequest;

public class DeleteAssetPhotoCommandHandler : IRequestHandler<DeleteAssetPhotoCommand>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IFileStorageService _fileStorage;

    public DeleteAssetPhotoCommandHandler(
        IApplicationDbContext dbContext,
        IFileStorageService fileStorage)
    {
        _dbContext = dbContext;
        _fileStorage = fileStorage;
    }

    public async Task Handle(DeleteAssetPhotoCommand request, CancellationToken cancellationToken)
    {
        var photo = await _dbContext.CafeAssetPhotos
            .FirstOrDefaultAsync(item => item.Id == request.PhotoId, cancellationToken)
            ?? throw new KeyNotFoundException("Photo was not found.");

        var wasPrimary = photo.IsPrimary;
        var assetId = photo.AssetId;
        var imageUrl = photo.ImageUrl;

        _dbContext.CafeAssetPhotos.Remove(photo);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _fileStorage.DeleteAssetPhotoAsync(imageUrl, cancellationToken);

        if (wasPrimary)
        {
            // promote the first remaining photo so the asset always has a cover
            var next = await _dbContext.CafeAssetPhotos
                .Where(item => item.AssetId == assetId)
                .FirstOrDefaultAsync(cancellationToken);

            if (next is not null)
            {
                next.MakePrimary();
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
