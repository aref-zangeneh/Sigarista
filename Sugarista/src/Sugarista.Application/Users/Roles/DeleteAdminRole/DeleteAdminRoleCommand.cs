using Microsoft.EntityFrameworkCore;
using Sugarista.Domain.Constants;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.Users.Roles.DeleteAdminRole;

public record DeleteAdminRoleCommand(Guid Id) : IRequest;

public class DeleteAdminRoleCommandHandler : IRequestHandler<DeleteAdminRoleCommand>
{
    private readonly IApplicationDbContext _dbContext;

    public DeleteAdminRoleCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(
        DeleteAdminRoleCommand request,
        CancellationToken cancellationToken)
    {
        var role = await _dbContext.AdminRoles
            .FirstOrDefaultAsync(entity => entity.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Role was not found.");

        if (role.IsSystem)
        {
            throw new DomainException("نقش‌های سیستمی قابل حذف نیستند.");
        }

        var assignedUsers = await _dbContext.AdminUsers
            .AsNoTracking()
            .CountAsync(user => user.Role == role.Name, cancellationToken);

        if (assignedUsers > 0)
        {
            throw new DomainException($"این نقش به {assignedUsers} کاربر تخصیص یافته است؛ ابتدا نقش کاربران را تغییر دهید.");
        }

        await _dbContext.AdminRoles
            .Where(entity => entity.Id == role.Id)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
