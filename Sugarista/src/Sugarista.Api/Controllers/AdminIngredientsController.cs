using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Api.Authorization;
using Sugarista.Application.Ingredients.Commands.DeleteIngredient;
using Sugarista.Application.Ingredients.Commands.UpsertIngredient;
using Sugarista.Application.Ingredients.Queries.GetAdminIngredients;
using Sugarista.Application.Ingredients.Queries.GetAdminIngredientOptions;
using Sugarista.Application.Common.Models;
using Sugarista.Domain.Constants;

namespace Sugarista.Api.Controllers;

[Authorize]
[Route("api/admin/ingredients")]
public class AdminIngredientsController : ApiControllerBase
{
    [HttpGet]
    [RequirePermission(AdminPermissions.Ingredients.View)]
    public async Task<ActionResult<PaginatedList<AdminIngredientDto>>> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(new GetAdminIngredientsQuery(page, pageSize, search), cancellationToken));
    }

    [HttpGet("options")]
    [RequireAnyPermission(
        AdminPermissions.Ingredients.View,
        AdminPermissions.Inventory.View,
        AdminPermissions.InventoryPurchases.View,
        AdminPermissions.Recipes.View)]
    public async Task<ActionResult<IReadOnlyCollection<AdminIngredientOptionDto>>> GetOptions(CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new GetAdminIngredientOptionsQuery(), cancellationToken));
    }

    [HttpPost]
    [RequirePermission(AdminPermissions.Ingredients.Create)]
    public async Task<ActionResult<Guid>> Create([FromBody] UpsertIngredientCommand command, CancellationToken cancellationToken)
    {
        var id = await Mediator.Send(command with { Id = null }, cancellationToken);
        return Ok(id);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(AdminPermissions.Ingredients.Update)]
    public async Task<ActionResult<Guid>> Update(Guid id, [FromBody] UpsertIngredientCommand command, CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command with { Id = id }, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(AdminPermissions.Ingredients.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteIngredientCommand(id), cancellationToken);
        return NoContent();
    }
}
