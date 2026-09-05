using Microsoft.AspNetCore.Authorization;

namespace Sugarista.Api.Authorization;

public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(IReadOnlyCollection<string> permissions, bool requireAll)
    {
        Permissions = permissions;
        RequireAll = requireAll;
    }

    public IReadOnlyCollection<string> Permissions { get; }

    public bool RequireAll { get; }
}

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        if (string.Equals(
                context.User.FindFirst("is_super_admin")?.Value,
                bool.TrueString,
                StringComparison.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var grantedPermissions = context.User
            .FindAll("permission")
            .Select(claim => claim.Value)
            .ToHashSet(StringComparer.Ordinal);

        var isAuthorized = requirement.RequireAll
            ? requirement.Permissions.All(grantedPermissions.Contains)
            : requirement.Permissions.Any(grantedPermissions.Contains);

        if (isAuthorized)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
