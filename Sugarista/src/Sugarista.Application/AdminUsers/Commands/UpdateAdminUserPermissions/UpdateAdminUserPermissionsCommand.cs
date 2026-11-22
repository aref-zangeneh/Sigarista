using Sugarista.Domain.Constants;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.AdminUsers.Commands.UpdateAdminUserPermissions;

public record UpdateAdminUserPermissionsCommand(Guid Id, IReadOnlyCollection<string> Permissions) : IRequest<Guid>;

public class UpdateAdminUserPermissionsCommandHandler : IRequestHandler<UpdateAdminUserPermissionsCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public UpdateAdminUserPermissionsCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<Guid> Handle(UpdateAdminUserPermissionsCommand request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.AdminUsers
            .Include(adminUser => adminUser.Permissions)
            .FirstOrDefaultAsync(adminUser => adminUser.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Admin user was not found.");

        if (user.IsSuperAdmin)
        {
            throw new InvalidOperationException("Super admins always have full access and cannot be restricted.");
        }

        if (!_currentUserService.IsSuperAdmin
            && Guid.TryParse(_currentUserService.UserId, out var currentUserId)
            && currentUserId == user.Id)
        {
            throw new InvalidOperationException("You cannot change your own permissions.");
        }

        if (user.Permissions.Count > 0)
        {
            _dbContext.AdminUserPermissions.RemoveRange(user.Permissions.ToList());
            user.Permissions.Clear();
        }

        var normalizedKeys = request.Permissions
            .Where(AdminPermissions.IsValid)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        foreach (var permissionKey in normalizedKeys)
        {
            var entry = new AdminUserPermission(user.Id, permissionKey);
            user.Permissions.Add(entry);
            _dbContext.AdminUserPermissions.Add(entry);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return user.Id;
    }
}
