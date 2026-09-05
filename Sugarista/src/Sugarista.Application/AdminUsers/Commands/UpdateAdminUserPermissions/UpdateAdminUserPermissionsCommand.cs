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

        user.ReplacePermissions(request.Permissions);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return user.Id;
    }
}
