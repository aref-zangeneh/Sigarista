namespace Sugarista.Application.AdminUsers.Commands.UpdateAdminUser;

public record UpdateAdminUserCommand(
    Guid Id,
    string DisplayName,
    string Role,
    bool IsActive,
    bool IsSuperAdmin) : IRequest<Guid>;

public class UpdateAdminUserCommandValidator : AbstractValidator<UpdateAdminUserCommand>
{
    public UpdateAdminUserCommandValidator()
    {
        RuleFor(command => command.DisplayName).NotEmpty().MaximumLength(150);
        RuleFor(command => command.Role).NotEmpty().MaximumLength(50);
    }
}

public class UpdateAdminUserCommandHandler : IRequestHandler<UpdateAdminUserCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public UpdateAdminUserCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<Guid> Handle(UpdateAdminUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.AdminUsers
            .Include(adminUser => adminUser.Permissions)
            .FirstOrDefaultAsync(adminUser => adminUser.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Admin user was not found.");

        if (Guid.TryParse(_currentUserService.UserId, out var currentUserId)
            && currentUserId == user.Id
            && !request.IsActive)
        {
            throw new InvalidOperationException("You cannot deactivate your own account.");
        }

        if (request.IsSuperAdmin && !_currentUserService.IsSuperAdmin)
        {
            throw new UnauthorizedAccessException("Only super admins can grant super admin access.");
        }

        if (user.IsSuperAdmin && !request.IsSuperAdmin && !_currentUserService.IsSuperAdmin)
        {
            throw new UnauthorizedAccessException("Only super admins can remove super admin access.");
        }

        user.UpdateProfile(user.Username, request.DisplayName, request.Role, request.IsActive, request.IsSuperAdmin);

        if (request.IsSuperAdmin)
        {
            user.ReplacePermissions([]);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return user.Id;
    }
}
