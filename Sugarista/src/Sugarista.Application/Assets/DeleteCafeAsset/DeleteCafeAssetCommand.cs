using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Assets.DeleteCafeAsset;

public record DeleteCafeAssetCommand(Guid Id) : IRequest;

public class DeleteCafeAssetCommandHandler : IRequestHandler<DeleteCafeAssetCommand>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IFileStorageService _fileStorage;

    public DeleteCafeAssetCommandHandler(
        IApplicationDbContext dbContext,
        IFileStorageService fileStorage)
    {
        _dbContext = dbContext;
        _fileStorage = fileStorage;
    }

    public async Task Handle(DeleteCafeAssetCommand request, CancellationToken cancellationToken)
    {
        var asset = await _dbContext.CafeAssets
            .Include(item => item.Photos)
            .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Asset was not found.");

        // remove the gallery files first so no orphans stay on disk
        foreach (var photo in asset.Photos)
        {
            await _fileStorage.DeleteAssetPhotoAsync(photo.ImageUrl, cancellationToken);
        }

        _dbContext.CafeAssets.Remove(asset);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
