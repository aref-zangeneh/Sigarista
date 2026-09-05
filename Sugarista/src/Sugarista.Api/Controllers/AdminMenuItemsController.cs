using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Api.Authorization;
using Sugarista.Application.MenuItems.Commands.DeleteMenuItem;
using Sugarista.Application.MenuItems.Commands.UpsertMenuItem;
using Sugarista.Application.MenuItems.Queries.GetAdminMenuItems;
using Sugarista.Application.MenuItems.Queries.GetAdminMenuItemOptions;
using Sugarista.Application.Common.Models;
using Sugarista.Domain.Constants;

namespace Sugarista.Api.Controllers;

[Authorize]
[Route("api/admin/menu-items")]
public class AdminMenuItemsController : ApiControllerBase
{
    [HttpGet]
    [RequirePermission(AdminPermissions.MenuItems.View)]
    public async Task<ActionResult<PaginatedList<AdminMenuItemDto>>> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(new GetAdminMenuItemsQuery(page, pageSize, search), cancellationToken));
    }

    [HttpGet("options")]
    [RequireAnyPermission(
        AdminPermissions.MenuItems.View,
        AdminPermissions.Recipes.View,
        AdminPermissions.Production.View,
        AdminPermissions.RecordSale.View,
        AdminPermissions.ProductWiki.View)]
    public async Task<ActionResult<IReadOnlyCollection<AdminMenuItemOptionDto>>> GetOptions(CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new GetAdminMenuItemOptionsQuery(), cancellationToken));
    }

    [HttpPost]
    [RequirePermission(AdminPermissions.MenuItems.Create)]
    public async Task<ActionResult<Guid>> Create([FromBody] UpsertMenuItemCommand command, CancellationToken cancellationToken)
    {
        var itemId = await Mediator.Send(command with { Id = null }, cancellationToken);
        return Ok(itemId);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(AdminPermissions.MenuItems.Update)]
    public async Task<ActionResult<Guid>> Update(Guid id, [FromBody] UpsertMenuItemCommand command, CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command with { Id = id }, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(AdminPermissions.MenuItems.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteMenuItemCommand(id), cancellationToken);
        return NoContent();
    }
}
