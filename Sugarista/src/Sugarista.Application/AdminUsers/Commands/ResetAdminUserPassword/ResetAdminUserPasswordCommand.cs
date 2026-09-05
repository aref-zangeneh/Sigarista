namespace Sugarista.Application.AdminUsers.Commands.ResetAdminUserPassword;

public record ResetAdminUserPasswordCommand(Guid Id, string NewPassword) : IRequest<Guid>;

public class ResetAdminUserPasswordCommandValidator : AbstractValidator<ResetAdminUserPasswordCommand>
{
    public ResetAdminUserPasswordCommandValidator()
    {
        RuleFor(command => command.NewPassword).NotEmpty().MinimumLength(8);
    }
}

public class ResetAdminUserPasswordCommandHandler : IRequestHandler<ResetAdminUserPasswordCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAdminPasswordService _adminPasswordService;

    public ResetAdminUserPasswordCommandHandler(
        IApplicationDbContext dbContext,
        IAdminPasswordService adminPasswordService)
    {
        _dbContext = dbContext;
        _adminPasswordService = adminPasswordService;
    }

    public async Task<Guid> Handle(ResetAdminUserPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.AdminUsers
            .FirstOrDefaultAsync(adminUser => adminUser.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Admin user was not found.");

        user.SetPasswordHash(_adminPasswordService.HashPassword(user, request.NewPassword));
        await _dbContext.SaveChangesAsync(cancellationToken);
        return user.Id;
    }
}
