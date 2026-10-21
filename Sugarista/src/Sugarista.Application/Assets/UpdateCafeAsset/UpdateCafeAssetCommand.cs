using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Assets.Dto;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Assets.UpdateCafeAsset;

public record UpdateCafeAssetCommand(
    Guid Id,
    string PersianName,
    string? EnglishName,
    string Category,
    string? SerialNumber,
    string Location,
    DateOnly? PurchaseDate,
    decimal UnitPrice,
    int Quantity,
    string? SupplierName,
    string? Note) : IRequest<CafeAssetDto>;

public class UpdateCafeAssetCommandHandler : IRequestHandler<UpdateCafeAssetCommand, CafeAssetDto>
{
    private readonly IApplicationDbContext _dbContext;

    public UpdateCafeAssetCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CafeAssetDto> Handle(
        UpdateCafeAssetCommand request,
        CancellationToken cancellationToken)
    {
        var asset = await _dbContext.CafeAssets
            .Include(item => item.Photos)
            .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Asset was not found.");

        asset.Update(
            request.PersianName,
            request.EnglishName,
            request.Category,
            request.SerialNumber,
            request.Location,
            request.PurchaseDate,
            request.UnitPrice,
            request.Quantity,
            request.SupplierName,
            request.Note);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return CafeAssetMapper.ToDto(asset);
    }
}
