using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Application.Modifiers.Commands.DeleteMenuModifier;
using Sugarista.Application.Modifiers.Commands.UpsertMenuModifier;
using Sugarista.Application.Modifiers.Queries.GetAdminMenuModifiers;
using Sugarista.Application.Common.Models;
using Sugarista.Domain.Constants;

namespace Sugarista.Api.Controllers;

[Authorize(Roles = RoleNames.Admin)]
[Route("api/admin/modifiers")]
public class AdminMenuModifiersController : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PaginatedList<AdminMenuModifierDto>>> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(new GetAdminMenuModifiersQuery(page, pageSize, search), cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<Guid>> Create([FromBody] UpsertMenuModifierCommand command, CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command with { Id = null }, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Guid>> Update(Guid id, [FromBody] UpsertMenuModifierCommand command, CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command with { Id = id }, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteMenuModifierCommand(id), cancellationToken);
        return NoContent();
    }
}
