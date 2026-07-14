using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Application.Common.Models;
using Sugarista.Application.Inventory.Commands.RecordProduction;
using Sugarista.Application.Inventory.Queries.GetProductionBatches;
using Sugarista.Application.Inventory.Queries.GetProductionStock;
using Sugarista.Domain.Constants;

namespace Sugarista.Api.Controllers;

[Authorize(Roles = RoleNames.Admin)]
[Route("api/admin/production")]
public class AdminProductionController : ApiControllerBase
{
    [HttpGet("batches")]
    public async Task<ActionResult<PaginatedList<ProductionBatchDto>>> GetBatches(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        [FromQuery] Guid? menuItemId = null,
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(
            new GetProductionBatchesQuery(page, pageSize, search, menuItemId),
            cancellationToken));
    }

    [HttpGet("stock")]
    public async Task<ActionResult<IReadOnlyCollection<ProductionStockDto>>> GetStock(
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(new GetProductionStockQuery(), cancellationToken));
    }

    [HttpPost("record")]
    public async Task<ActionResult<RecordProductionResultDto>> Record(
        [FromBody] RecordProductionCommand command,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return Ok(result);
    }
}
