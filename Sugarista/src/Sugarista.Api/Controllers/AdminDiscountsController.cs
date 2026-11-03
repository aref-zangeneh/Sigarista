using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Api.Authorization;
using Sugarista.Application.Common.Models;
using Sugarista.Application.Discounts.Commands.DeleteDiscount;
using Sugarista.Application.Discounts.Commands.UpsertDiscount;
using Sugarista.Application.Discounts.Queries.GetAdminDiscounts;
using Sugarista.Domain.Constants;

namespace Sugarista.Api.Controllers;

[Authorize]
[Route("api/admin/discounts")]
public class AdminDiscountsController : ApiControllerBase
{
    [HttpGet]
    [RequirePermission(AdminPermissions.Discounts.View)]
    public async Task<ActionResult<PaginatedList<AdminDiscountDto>>> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(new GetAdminDiscountsQuery(page, pageSize, search), cancellationToken));
    }

    [HttpPost]
    [RequirePermission(AdminPermissions.Discounts.Create)]
    public async Task<ActionResult<Guid>> Create([FromBody] UpsertDiscountCommand command, CancellationToken cancellationToken)
    {
        var id = await Mediator.Send(command with { Id = null }, cancellationToken);
        return Ok(id);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(AdminPermissions.Discounts.Update)]
    public async Task<ActionResult<Guid>> Update(Guid id, [FromBody] UpsertDiscountCommand command, CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command with { Id = id }, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(AdminPermissions.Discounts.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteDiscountCommand(id), cancellationToken);
        return NoContent();
    }
}
