namespace Sugarista.Application.AdminUsers.Queries.GetAdminUserById;

public record AdminUserDetailDto(
    Guid Id,
    string Username,
    string DisplayName,
    string Role,
    bool IsActive,
    bool IsSuperAdmin,
    IReadOnlyCollection<string> Permissions,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastModifiedAt);

public record GetAdminUserByIdQuery(Guid Id) : IRequest<AdminUserDetailDto>;

public class GetAdminUserByIdQueryHandler : IRequestHandler<GetAdminUserByIdQuery, AdminUserDetailDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAdminAuthorizationService _adminAuthorizationService;

    public GetAdminUserByIdQueryHandler(
        IApplicationDbContext dbContext,
        IAdminAuthorizationService adminAuthorizationService)
    {
        _dbContext = dbContext;
        _adminAuthorizationService = adminAuthorizationService;
    }

    public async Task<AdminUserDetailDto> Handle(GetAdminUserByIdQuery request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.AdminUsers
            .AsNoTracking()
            .Include(adminUser => adminUser.Permissions)
            .FirstOrDefaultAsync(adminUser => adminUser.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Admin user was not found.");

        var permissions = await _adminAuthorizationService.GetPermissionsAsync(user.Id, cancellationToken);

        return new AdminUserDetailDto(
            user.Id,
            user.Username,
            user.DisplayName,
            user.Role,
            user.IsActive,
            user.IsSuperAdmin,
            permissions,
            user.CreatedAt,
            user.LastModifiedAt);
    }
}
