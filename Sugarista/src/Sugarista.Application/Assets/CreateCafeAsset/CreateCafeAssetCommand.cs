using Sugarista.Application.Assets.Dto;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Assets.CreateCafeAsset;

public record CreateCafeAssetCommand(
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

public class CreateCafeAssetCommandHandler : IRequestHandler<CreateCafeAssetCommand, CafeAssetDto>
{
    private readonly IApplicationDbContext _dbContext;

    public CreateCafeAssetCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CafeAssetDto> Handle(
        CreateCafeAssetCommand request,
        CancellationToken cancellationToken)
    {
        var asset = new CafeAsset(
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

        _dbContext.CafeAssets.Add(asset);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return CafeAssetMapper.ToDto(asset);
    }
}
