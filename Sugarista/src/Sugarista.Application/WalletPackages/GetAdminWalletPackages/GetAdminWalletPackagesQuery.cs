using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.WalletPackages;

public record AdminWalletPackageDto(
    Guid Id,
    decimal Amount,
    decimal BonusAmount,
    int DisplayOrder,
    bool IsEnabled);

public record GetAdminWalletPackagesQuery : IRequest<IReadOnlyCollection<AdminWalletPackageDto>>;

public class GetAdminWalletPackagesQueryHandler
    : IRequestHandler<GetAdminWalletPackagesQuery, IReadOnlyCollection<AdminWalletPackageDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetAdminWalletPackagesQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<AdminWalletPackageDto>> Handle(
        GetAdminWalletPackagesQuery request,
        CancellationToken cancellationToken)
    {
        var packages = await _dbContext.WalletTopUpPackages
            .AsNoTracking()
            .OrderBy(packageEntry => packageEntry.DisplayOrder)
            .ThenBy(packageEntry => packageEntry.Amount)
            .ToListAsync(cancellationToken);

        return packages
            .Select(packageEntry => new AdminWalletPackageDto(
                packageEntry.Id,
                packageEntry.Amount,
                packageEntry.BonusAmount,
                packageEntry.DisplayOrder,
                packageEntry.IsEnabled))
            .ToList();
    }
}
