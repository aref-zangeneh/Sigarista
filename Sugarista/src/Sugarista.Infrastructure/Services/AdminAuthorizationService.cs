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

        // union of the user's own permission rows and the bundle of the role
        // he/she is assigned to — editing a role's bundle takes effect for all
        // members on their next login.
        var rolePermissions = await _dbContext.AdminRoles
            .AsNoTracking()
            .Where(entity => entity.Name == user.Role)
            .SelectMany(entity => entity.Permissions)
            .Select(permission => permission.PermissionKey)
            .ToListAsync(cancellationToken);

        return user.Permissions
            .Select(permission => permission.PermissionKey)
            .Concat(rolePermissions)
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
