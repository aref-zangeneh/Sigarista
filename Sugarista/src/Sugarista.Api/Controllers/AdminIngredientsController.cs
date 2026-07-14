using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Application.Ingredients.Commands.DeleteIngredient;
using Sugarista.Application.Ingredients.Commands.UpsertIngredient;
using Sugarista.Application.Ingredients.Queries.GetAdminIngredients;
using Sugarista.Application.Ingredients.Queries.GetAdminIngredientOptions;
using Sugarista.Application.Common.Models;
using Sugarista.Domain.Constants;

namespace Sugarista.Api.Controllers;

[Authorize(Roles = RoleNames.Admin)]
[Route("api/admin/ingredients")]
public class AdminIngredientsController : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PaginatedList<AdminIngredientDto>>> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(new GetAdminIngredientsQuery(page, pageSize, search), cancellationToken));
    }

    [HttpGet("options")]
    public async Task<ActionResult<IReadOnlyCollection<AdminIngredientOptionDto>>> GetOptions(CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new GetAdminIngredientOptionsQuery(), cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<Guid>> Create([FromBody] UpsertIngredientCommand command, CancellationToken cancellationToken)
    {
        var id = await Mediator.Send(command with { Id = null }, cancellationToken);
        return Ok(id);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Guid>> Update(Guid id, [FromBody] UpsertIngredientCommand command, CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command with { Id = id }, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteIngredientCommand(id), cancellationToken);
        return NoContent();
    }
}
