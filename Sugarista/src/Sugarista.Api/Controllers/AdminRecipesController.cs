using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Application.Recipes.Commands.DeleteRecipeItem;
using Sugarista.Application.Recipes.Commands.UpsertRecipeItem;
using Sugarista.Application.Recipes.Queries.GetMenuItemRecipe;
using Sugarista.Domain.Constants;

namespace Sugarista.Api.Controllers;

[Authorize(Roles = RoleNames.Admin)]
[Route("api/admin/recipes")]
public class AdminRecipesController : ApiControllerBase
{
    [HttpGet("{menuItemId:guid}")]
    public async Task<ActionResult<IReadOnlyCollection<RecipeItemDto>>> GetByMenuItem(Guid menuItemId, CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new GetMenuItemRecipeQuery(menuItemId), cancellationToken));
    }

    [HttpPost("{menuItemId:guid}")]
    public async Task<ActionResult<Guid>> Upsert(Guid menuItemId, [FromBody] UpsertRecipeItemCommand command, CancellationToken cancellationToken)
    {
        var id = await Mediator.Send(command with { MenuItemId = menuItemId }, cancellationToken);
        return Ok(id);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteRecipeItemCommand(id), cancellationToken);
        return NoContent();
    }
}
