using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Constants;

namespace Sugarista.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public string? UserId => User?.FindFirstValue(ClaimTypes.NameIdentifier);

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;

    public bool IsSuperAdmin =>
        string.Equals(
            User?.FindFirstValue(AdminPermissions.SuperAdminClaimType),
            bool.TrueString,
            StringComparison.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> Permissions =>
        User?.FindAll(AdminPermissions.ClaimType)
            .Select(claim => claim.Value)
            .Distinct(StringComparer.Ordinal)
            .ToList()
        ?? [];

    public bool HasPermission(string permission)
    {
        if (!IsAuthenticated)
        {
            return false;
        }

        if (IsSuperAdmin)
        {
            return true;
        }

        return Permissions.Contains(permission, StringComparer.Ordinal);
    }
}
