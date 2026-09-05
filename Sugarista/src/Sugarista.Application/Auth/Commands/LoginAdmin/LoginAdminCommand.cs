using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Constants;

namespace Sugarista.Application.Auth.Commands.LoginAdmin;

public record LoginAdminCommand(string Username, string Password) : IRequest<AdminLoginResultDto>;

public record AdminLoginResultDto(
    string Token,
    Guid UserId,
    string Username,
    string DisplayName,
    string Role,
    bool IsSuperAdmin,
    IReadOnlyCollection<string> Permissions);

public class LoginAdminCommandValidator : AbstractValidator<LoginAdminCommand>
{
    public LoginAdminCommandValidator()
    {
        RuleFor(command => command.Username).NotEmpty().MaximumLength(100);
        RuleFor(command => command.Password).NotEmpty().MinimumLength(8);
    }
}

public class LoginAdminCommandHandler : IRequestHandler<LoginAdminCommand, AdminLoginResultDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAdminPasswordService _adminPasswordService;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IAdminAuthorizationService _adminAuthorizationService;

    public LoginAdminCommandHandler(
        IApplicationDbContext dbContext,
        IAdminPasswordService adminPasswordService,
        IJwtTokenGenerator jwtTokenGenerator,
        IAdminAuthorizationService adminAuthorizationService)
    {
        _dbContext = dbContext;
        _adminPasswordService = adminPasswordService;
        _jwtTokenGenerator = jwtTokenGenerator;
        _adminAuthorizationService = adminAuthorizationService;
    }

    public async Task<AdminLoginResultDto> Handle(LoginAdminCommand request, CancellationToken cancellationToken)
    {
        var normalizedUsername = request.Username.Trim().ToLowerInvariant();

        var user = await _dbContext.AdminUsers
            .Include(adminUser => adminUser.Permissions)
            .FirstOrDefaultAsync(admin => admin.Username == normalizedUsername && admin.IsActive, cancellationToken);

        if (user is null || !_adminPasswordService.VerifyPassword(user, request.Password))
        {
            throw new UnauthorizedAccessException("Invalid username or password.");
        }

        var permissions = await _adminAuthorizationService.GetPermissionsAsync(user.Id, cancellationToken);
        var token = _jwtTokenGenerator.GenerateToken(user, permissions);

        return new AdminLoginResultDto(
            token,
            user.Id,
            user.Username,
            user.DisplayName,
            user.Role,
            user.IsSuperAdmin,
            permissions);
    }
}
