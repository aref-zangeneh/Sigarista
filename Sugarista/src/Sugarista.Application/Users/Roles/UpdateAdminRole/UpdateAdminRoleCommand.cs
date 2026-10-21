using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Users.Roles.GetAdminRoles;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.Users.Roles.UpdateAdminRole;

public record UpdateAdminRoleCommand(
    Guid Id,
    string DisplayName,
    IReadOnlyList<string> Permissions) : IRequest<AdminRoleDto>;

public class UpdateAdminRoleCommandHandler : IRequestHandler<UpdateAdminRoleCommand, AdminRoleDto>
{
    private readonly IApplicationDbContext _dbContext;

    public UpdateAdminRoleCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AdminRoleDto> Handle(
        UpdateAdminRoleCommand request,
        CancellationToken cancellationToken)
    {
        var role = await _dbContext.AdminRoles
            .FirstOrDefaultAsync(entity => entity.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Role was not found.");

        role.Update(request.DisplayName);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // explicit replace: pinned to a plain delete + fresh inserts to keep the
        // change tracker unambiguous about old vs new permission rows
        await _dbContext.AdminRolePermissions
            .Where(permission => permission.AdminRoleId == role.Id)
            .ExecuteDeleteAsync(cancellationToken);

        foreach (var key in request.Permissions.Distinct(StringComparer.Ordinal))
        {
            _dbContext.AdminRolePermissions.Add(new AdminRolePermission(role.Id, key));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var userCount = await _dbContext.AdminUsers
            .AsNoTracking()
            .CountAsync(user => user.Role == role.Name, cancellationToken);

        return new AdminRoleDto(
            role.Id,
            role.Name,
            role.DisplayName,
            role.IsSystem,
            userCount,
            role.Permissions
                .Select(permission => permission.PermissionKey)
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToList());
    }
}
