using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Api.Authorization;
using Sugarista.Application.AdminUsers.Commands.CreateAdminUser;
using Sugarista.Application.AdminUsers.Commands.DeactivateAdminUser;
using Sugarista.Application.AdminUsers.Commands.ResetAdminUserPassword;
using Sugarista.Application.AdminUsers.Commands.UpdateAdminUser;
using Sugarista.Application.AdminUsers.Commands.UpdateAdminUserPermissions;
using Sugarista.Application.AdminUsers.Queries.GetAdminPermissionCatalog;
using Sugarista.Application.AdminUsers.Queries.GetAdminUserById;
using Sugarista.Application.AdminUsers.Queries.GetAdminUsers;
using Sugarista.Domain.Constants;

namespace Sugarista.Api.Controllers;

[Authorize]
[Route("api/admin/users")]
public class AdminUsersController : ApiControllerBase
{
    [HttpGet("permissions/catalog")]
    [RequirePermission(AdminPermissions.Users.View)]
    public async Task<ActionResult<IReadOnlyCollection<AdminPermissionGroupDto>>> GetPermissionCatalog(
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new GetAdminPermissionCatalogQuery(), cancellationToken));
    }

    [HttpGet]
    [RequirePermission(AdminPermissions.Users.View)]
    public async Task<ActionResult<IReadOnlyCollection<AdminUserListItemDto>>> Get(CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new GetAdminUsersQuery(), cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(AdminPermissions.Users.View)]
    public async Task<ActionResult<AdminUserDetailDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new GetAdminUserByIdQuery(id), cancellationToken));
    }

    [HttpPost]
    [RequirePermission(AdminPermissions.Users.Create)]
    public async Task<ActionResult<Guid>> Create(
        [FromBody] CreateAdminUserCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(AdminPermissions.Users.Update)]
    public async Task<ActionResult<Guid>> Update(
        Guid id,
        [FromBody] UpdateAdminUserCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command with { Id = id }, cancellationToken));
    }

    [HttpPut("{id:guid}/permissions")]
    [RequirePermission(AdminPermissions.Users.ManagePermissions)]
    public async Task<ActionResult<Guid>> UpdatePermissions(
        Guid id,
        [FromBody] UpdateAdminUserPermissionsRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(
            new UpdateAdminUserPermissionsCommand(id, request.Permissions),
            cancellationToken));
    }

    [HttpPut("{id:guid}/password")]
    [RequirePermission(AdminPermissions.Users.Update)]
    public async Task<ActionResult<Guid>> ResetPassword(
        Guid id,
        [FromBody] ResetAdminUserPasswordRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(
            new ResetAdminUserPasswordCommand(id, request.NewPassword),
            cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(AdminPermissions.Users.Delete)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeactivateAdminUserCommand(id), cancellationToken);
        return NoContent();
    }
}

public record UpdateAdminUserPermissionsRequest(IReadOnlyCollection<string> Permissions);

public record ResetAdminUserPasswordRequest(string NewPassword);
