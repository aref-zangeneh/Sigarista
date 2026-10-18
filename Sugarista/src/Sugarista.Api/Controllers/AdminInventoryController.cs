using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Api.Authorization;
using Sugarista.Application.Inventory.Commands.AdjustStock;
using Sugarista.Application.Inventory.Commands.RestockIngredient;
using Sugarista.Application.Inventory.Commands.SetAlertThreshold;
using Sugarista.Application.Inventory.Queries.GetInventoryConsumption;
using Sugarista.Application.Inventory.Queries.GetInventoryStatus;
using Sugarista.Application.Inventory.Queries.GetStockHistory;
using Sugarista.Domain.Constants;

namespace Sugarista.Api.Controllers;

[Authorize]
[Route("api/admin/inventory")]
public class AdminInventoryController : ApiControllerBase
{
    [HttpGet]
    [RequirePermission(AdminPermissions.Inventory.View)]
    public async Task<ActionResult<InventoryStatusDto>> GetStatus(CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new GetInventoryStatusQuery(), cancellationToken));
    }

    [HttpGet("consumption")]
    [RequirePermission(AdminPermissions.Inventory.View)]
    public async Task<ActionResult<IReadOnlyCollection<IngredientConsumptionDto>>> GetConsumption(
        [FromQuery] int days = 30,
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(new GetInventoryConsumptionQuery(days), cancellationToken));
    }

    [HttpGet("{ingredientId:guid}/history")]
    [RequirePermission(AdminPermissions.Inventory.View)]
    public async Task<ActionResult<IReadOnlyCollection<StockMovementDto>>> GetHistory(
        Guid ingredientId,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(new GetStockHistoryQuery(ingredientId, pageSize), cancellationToken));
    }

    [HttpPost("{ingredientId:guid}/restock")]
    [RequirePermission(AdminPermissions.Inventory.Create)]
    public async Task<IActionResult> Restock(Guid ingredientId, [FromBody] RestockIngredientCommand command, CancellationToken cancellationToken)
    {
        await Mediator.Send(command with { IngredientId = ingredientId }, cancellationToken);
        return NoContent();
    }

    [HttpPost("{ingredientId:guid}/adjust")]
    [RequirePermission(AdminPermissions.Inventory.Update)]
    public async Task<IActionResult> Adjust(Guid ingredientId, [FromBody] AdjustStockCommand command, CancellationToken cancellationToken)
    {
        await Mediator.Send(command with { IngredientId = ingredientId }, cancellationToken);
        return NoContent();
    }

    [HttpPost("{ingredientId:guid}/alert-threshold")]
    [RequirePermission(AdminPermissions.Inventory.Update)]
    public async Task<IActionResult> SetAlertThreshold(Guid ingredientId, [FromBody] SetAlertThresholdCommand command, CancellationToken cancellationToken)
    {
        await Mediator.Send(command with { IngredientId = ingredientId }, cancellationToken);
        return NoContent();
    }
}
