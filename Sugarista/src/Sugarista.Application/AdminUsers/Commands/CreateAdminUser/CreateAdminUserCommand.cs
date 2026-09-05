using Sugarista.Domain.Constants;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.AdminUsers.Commands.CreateAdminUser;

public record CreateAdminUserCommand(
    string Username,
    string DisplayName,
    string Password,
    string Role,
    bool IsActive,
    bool IsSuperAdmin,
    IReadOnlyCollection<string> Permissions) : IRequest<Guid>;

public class CreateAdminUserCommandValidator : AbstractValidator<CreateAdminUserCommand>
{
    public CreateAdminUserCommandValidator()
    {
        RuleFor(command => command.Username).NotEmpty().MaximumLength(100);
        RuleFor(command => command.DisplayName).NotEmpty().MaximumLength(150);
        RuleFor(command => command.Password).NotEmpty().MinimumLength(8);
        RuleFor(command => command.Role).NotEmpty().MaximumLength(50);
    }
}

public class CreateAdminUserCommandHandler : IRequestHandler<CreateAdminUserCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAdminPasswordService _adminPasswordService;
    private readonly ICurrentUserService _currentUserService;

    public CreateAdminUserCommandHandler(
        IApplicationDbContext dbContext,
        IAdminPasswordService adminPasswordService,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _adminPasswordService = adminPasswordService;
        _currentUserService = currentUserService;
    }

    public async Task<Guid> Handle(CreateAdminUserCommand request, CancellationToken cancellationToken)
    {
        if (request.IsSuperAdmin && !_currentUserService.IsSuperAdmin)
        {
            throw new UnauthorizedAccessException("Only super admins can create other super admins.");
        }

        var normalizedUsername = request.Username.Trim().ToLowerInvariant();
        var usernameExists = await _dbContext.AdminUsers
            .AnyAsync(user => user.Username == normalizedUsername, cancellationToken);

        if (usernameExists)
        {
            throw new InvalidOperationException("An admin user with this username already exists.");
        }

        var user = new AdminUser(
            normalizedUsername,
            request.DisplayName,
            passwordHash: "placeholder",
            request.Role,
            request.IsActive);

        if (request.IsSuperAdmin)
        {
            user.SetSuperAdmin(true);
        }
        else
        {
            user.ReplacePermissions(request.Permissions);
        }

        user.SetPasswordHash(_adminPasswordService.HashPassword(user, request.Password));

        _dbContext.AdminUsers.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return user.Id;
    }
}
