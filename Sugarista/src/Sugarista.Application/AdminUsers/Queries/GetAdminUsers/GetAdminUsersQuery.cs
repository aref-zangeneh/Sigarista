namespace Sugarista.Application.AdminUsers.Queries.GetAdminUsers;

using Sugarista.Domain.Constants;

public record AdminUserListItemDto(
    Guid Id,
    string Username,
    string DisplayName,
    string Role,
    bool IsActive,
    bool IsSuperAdmin,
    int PermissionCount,
    DateTimeOffset CreatedAt);

public record GetAdminUsersQuery : IRequest<IReadOnlyCollection<AdminUserListItemDto>>;

public class GetAdminUsersQueryHandler : IRequestHandler<GetAdminUsersQuery, IReadOnlyCollection<AdminUserListItemDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetAdminUsersQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<AdminUserListItemDto>> Handle(
        GetAdminUsersQuery request,
        CancellationToken cancellationToken)
    {
        var users = await _dbContext.AdminUsers
            .AsNoTracking()
            .Include(user => user.Permissions)
            .OrderBy(user => user.DisplayName)
            .ToListAsync(cancellationToken);

        return users
            .Select(user => new AdminUserListItemDto(
                user.Id,
                user.Username,
                user.DisplayName,
                user.Role,
                user.IsActive,
                user.IsSuperAdmin,
                user.IsSuperAdmin ? AdminPermissions.AllKeys.Count : user.Permissions.Count,
                user.CreatedAt))
            .ToList();
    }
}
