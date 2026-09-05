using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;

namespace Sugarista.Infrastructure.Services;

public class AdminAuthorizationService : IAdminAuthorizationService
{
    private readonly IApplicationDbContext _dbContext;

    public AdminAuthorizationService(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<string>> GetPermissionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.AdminUsers
            .AsNoTracking()
            .Include(adminUser => adminUser.Permissions)
            .FirstOrDefaultAsync(adminUser => adminUser.Id == userId, cancellationToken);

        if (user is null)
        {
            return [];
        }

        if (user.IsSuperAdmin)
        {
            return Domain.Constants.AdminPermissions.AllKeys.ToList();
        }

        return user.Permissions
            .Select(permission => permission.PermissionKey)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    public async Task<bool> IsSuperAdminAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.AdminUsers
            .AsNoTracking()
            .AnyAsync(adminUser => adminUser.Id == userId && adminUser.IsSuperAdmin, cancellationToken);
    }
}
