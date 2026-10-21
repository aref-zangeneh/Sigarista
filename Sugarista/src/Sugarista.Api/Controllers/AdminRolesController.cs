using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Api.Authorization;
using Sugarista.Application.Users.Roles.CreateAdminRole;
using Sugarista.Application.Users.Roles.DeleteAdminRole;
using Sugarista.Application.Users.Roles.GetAdminRoles;
using Sugarista.Application.Users.Roles.UpdateAdminRole;
using Sugarista.Domain.Constants;

namespace Sugarista.Api.Controllers;

/// <summary>
/// Security roles management (نقش‌های دسترسی). The four seeded system roles
/// keep financial data separation; merchants can add their own.
/// </summary>
[Authorize]
[Route("api/admin/users/roles")]
public class AdminRolesController : ApiControllerBase
{
    [HttpGet]
    [RequirePermission(AdminPermissions.Users.View)]
    public async Task<IActionResult> GetRoles(CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new GetAdminRolesQuery(), cancellationToken));
    }

    [HttpPost]
    [RequirePermission(AdminPermissions.Users.ManagePermissions)]
    public async Task<IActionResult> CreateRole(
        [FromBody] CreateAdminRoleCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(AdminPermissions.Users.ManagePermissions)]
    public async Task<IActionResult> UpdateRole(
        Guid id,
        [FromBody] UpdateAdminRoleRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new UpdateAdminRoleCommand(
            id,
            request.DisplayName,
            request.Permissions), cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(AdminPermissions.Users.ManagePermissions)]
    public async Task<IActionResult> DeleteRole(
        Guid id,
        CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteAdminRoleCommand(id), cancellationToken);
        return NoContent();
    }
}

public record UpdateAdminRoleRequest(
    string DisplayName,
    IReadOnlyList<string> Permissions);
