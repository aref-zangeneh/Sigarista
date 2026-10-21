using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Users.Roles.GetAdminRoles;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.Users.Roles.CreateAdminRole;

public record CreateAdminRoleCommand(
    string Name,
    string DisplayName,
    IReadOnlyList<string> Permissions) : IRequest<AdminRoleDto>;

public class CreateAdminRoleCommandHandler : IRequestHandler<CreateAdminRoleCommand, AdminRoleDto>
{
    private readonly IApplicationDbContext _dbContext;

    public CreateAdminRoleCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AdminRoleDto> Handle(
        CreateAdminRoleCommand request,
        CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (name.Length is 0 or > 60)
        {
            throw new DomainException("نام نقش باید بین ۱ تا ۶۰ کاراکتر باشد.");
        }

        var duplicated = await _dbContext.AdminRoles
            .AnyAsync(entity => entity.Name == name, cancellationToken);
        if (duplicated)
        {
            throw new DomainException("نقشی با این نام وجود دارد.");
        }

        var role = new AdminRole(name, request.DisplayName, isSystem: false);
        role.ReplacePermissions(request.Permissions);

        _dbContext.AdminRoles.Add(role);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new AdminRoleDto(
            role.Id,
            role.Name,
            role.DisplayName,
            role.IsSystem,
            0,
            role.Permissions
                .Select(permission => permission.PermissionKey)
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToList());
    }
}
