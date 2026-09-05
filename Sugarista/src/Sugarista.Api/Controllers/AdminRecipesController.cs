using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Api.Authorization;
using Sugarista.Application.Recipes.Commands.DeleteRecipeItem;
using Sugarista.Application.Recipes.Commands.UpdateMenuItemRecipeDescription;
using Sugarista.Application.Recipes.Commands.UpsertRecipeItem;
using Sugarista.Application.Recipes.Queries.GetMenuItemRecipe;
using Sugarista.Domain.Constants;

namespace Sugarista.Api.Controllers;

[Authorize]
[Route("api/admin/recipes")]
public class AdminRecipesController : ApiControllerBase
{
    [HttpGet("{menuItemId:guid}")]
    [RequirePermission(AdminPermissions.Recipes.View)]
    public async Task<ActionResult<MenuItemRecipeDto>> GetByMenuItem(Guid menuItemId, CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new GetMenuItemRecipeQuery(menuItemId), cancellationToken));
    }

    [HttpPut("{menuItemId:guid}/description")]
    [RequirePermission(AdminPermissions.Recipes.Update)]
    public async Task<IActionResult> UpdateDescription(
        Guid menuItemId,
        [FromBody] UpdateMenuItemRecipeDescriptionRequest request,
        CancellationToken cancellationToken)
    {
        await Mediator.Send(new UpdateMenuItemRecipeDescriptionCommand(menuItemId, request.Description), cancellationToken);
        return NoContent();
    }

    [HttpPost("{menuItemId:guid}")]
    [RequirePermission(AdminPermissions.Recipes.Create)]
    public async Task<ActionResult<Guid>> Upsert(Guid menuItemId, [FromBody] UpsertRecipeItemCommand command, CancellationToken cancellationToken)
    {
        var id = await Mediator.Send(command with { MenuItemId = menuItemId }, cancellationToken);
        return Ok(id);
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(AdminPermissions.Recipes.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteRecipeItemCommand(id), cancellationToken);
        return NoContent();
    }
}

public record UpdateMenuItemRecipeDescriptionRequest(string? Description);
