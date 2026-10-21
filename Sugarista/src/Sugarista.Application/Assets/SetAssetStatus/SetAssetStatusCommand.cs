using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Assets.Dto;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Assets.SetAssetStatus;

/// <summary>Takes the asset out of service (افست) or restores it (بازگشت به سرویس).</summary>
public record SetAssetStatusCommand(
    Guid Id,
    bool IsActive,
    string? DisposalNote) : IRequest<CafeAssetDto>;

public class SetAssetStatusCommandHandler : IRequestHandler<SetAssetStatusCommand, CafeAssetDto>
{
    private readonly IApplicationDbContext _dbContext;

    public SetAssetStatusCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CafeAssetDto> Handle(
        SetAssetStatusCommand request,
        CancellationToken cancellationToken)
    {
        var asset = await _dbContext.CafeAssets
            .Include(item => item.Photos)
            .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Asset was not found.");

        if (request.IsActive)
        {
            if (asset.IsActive)
            {
                throw new Sugarista.Domain.Exceptions.DomainException("این قلم هم‌اکنون در سرویس است.");
            }

            asset.Restore();
        }
        else
        {
            if (!asset.IsActive)
            {
                throw new Sugarista.Domain.Exceptions.DomainException("این قلم قبلاً خارج از رده شده است.");
            }

            asset.Retire(request.DisposalNote);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return CafeAssetMapper.ToDto(asset);
    }
}
