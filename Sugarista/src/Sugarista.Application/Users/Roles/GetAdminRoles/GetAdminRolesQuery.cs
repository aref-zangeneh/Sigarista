using Microsoft.EntityFrameworkCore;
using Sugarista.Domain.Constants;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.Users.Roles.GetAdminRoles;

public record AdminRoleDto(
    Guid Id,
    string Name,
    string DisplayName,
    bool IsSystem,
    int UserCount,
    IReadOnlyList<string> Permissions);

public record GetAdminRolesQuery : IRequest<IReadOnlyList<AdminRoleDto>>;

public class GetAdminRolesQueryHandler : IRequestHandler<GetAdminRolesQuery, IReadOnlyList<AdminRoleDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetAdminRolesQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<AdminRoleDto>> Handle(
        GetAdminRolesQuery request,
        CancellationToken cancellationToken)
    {
        var roles = await _dbContext.AdminRoles
            .AsNoTracking()
            .Include(entity => entity.Permissions)
            .OrderBy(entity => entity.IsSystem ? 0 : 1)
            .ThenBy(entity => entity.Name)
            .ToListAsync(cancellationToken);

        var counts = await _dbContext.AdminUsers
            .AsNoTracking()
            .GroupBy(user => user.Role)
            .Select(group => new { Role = group.Key, Count = group.Count() })
            .ToDictionaryAsync(pair => pair.Role, pair => pair.Count, cancellationToken);

        return roles.Select(entity => new AdminRoleDto(
            entity.Id,
            entity.Name,
            entity.DisplayName,
            entity.IsSystem,
            counts.GetValueOrDefault(entity.Name, 0),
            entity.Permissions
                .Select(permission => permission.PermissionKey)
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToList())).ToList();
    }
}
