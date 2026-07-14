using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Application.Common.Models;
using Sugarista.Application.Inventory.Commands.RecordInventoryPurchase;
using Sugarista.Application.Inventory.Queries.GetInventoryPurchases;
using Sugarista.Domain.Constants;

namespace Sugarista.Api.Controllers;

[Authorize(Roles = RoleNames.Admin)]
[Route("api/admin/inventory/purchases")]
public class AdminInventoryPurchasesController : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PaginatedList<InventoryPurchaseDto>>> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        [FromQuery] Guid? ingredientId = null,
        [FromQuery] bool expiringOnly = false,
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(
            new GetInventoryPurchasesQuery(page, pageSize, search, ingredientId, expiringOnly),
            cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<Guid>> Create(
        [FromBody] RecordInventoryPurchaseCommand command,
        CancellationToken cancellationToken)
    {
        var purchaseId = await Mediator.Send(command, cancellationToken);
        return Ok(purchaseId);
    }
}
