using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Api.Authorization;
using Sugarista.Application.Addons.Commands.DeleteAddonItem;
using Sugarista.Application.Addons.Commands.UpsertAddonItem;
using Sugarista.Application.Addons.Queries.GetAdminAddonItems;
using Sugarista.Application.Common.Models;
using Sugarista.Domain.Constants;

namespace Sugarista.Api.Controllers;

[Authorize]
[Route("api/admin/addons")]
public class AdminAddonItemsController : ApiControllerBase
{
    [HttpGet]
    [RequirePermission(AdminPermissions.Addons.View)]
    public async Task<ActionResult<PaginatedList<AdminAddonItemDto>>> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(new GetAdminAddonItemsQuery(page, pageSize, search), cancellationToken));
    }

    [HttpPost]
    [RequirePermission(AdminPermissions.Addons.Create)]
    public async Task<ActionResult<Guid>> Create([FromBody] UpsertAddonItemCommand command, CancellationToken cancellationToken)
    {
        var id = await Mediator.Send(command with { Id = null }, cancellationToken);
        return Ok(id);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(AdminPermissions.Addons.Update)]
    public async Task<ActionResult<Guid>> Update(Guid id, [FromBody] UpsertAddonItemCommand command, CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command with { Id = id }, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(AdminPermissions.Addons.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteAddonItemCommand(id), cancellationToken);
        return NoContent();
    }
}
