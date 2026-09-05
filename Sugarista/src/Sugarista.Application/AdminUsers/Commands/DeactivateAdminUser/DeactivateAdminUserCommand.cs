namespace Sugarista.Application.AdminUsers.Commands.DeactivateAdminUser;

public record DeactivateAdminUserCommand(Guid Id) : IRequest<Guid>;

public class DeactivateAdminUserCommandHandler : IRequestHandler<DeactivateAdminUserCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public DeactivateAdminUserCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<Guid> Handle(DeactivateAdminUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.AdminUsers
            .FirstOrDefaultAsync(adminUser => adminUser.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Admin user was not found.");

        if (Guid.TryParse(_currentUserService.UserId, out var currentUserId) && currentUserId == user.Id)
        {
            throw new InvalidOperationException("You cannot deactivate your own account.");
        }

        user.UpdateProfile(user.Username, user.DisplayName, user.Role, isActive: false, user.IsSuperAdmin);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return user.Id;
    }
}
