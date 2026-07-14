using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Application.Inventory.Commands.RecordSale;
using Sugarista.Domain.Constants;

namespace Sugarista.Api.Controllers;

[Authorize(Roles = RoleNames.Admin)]
[Route("api/admin/sales")]
public class AdminSalesController : ApiControllerBase
{
    [HttpPost("record")]
    public async Task<ActionResult<RecordSaleResultDto>> Record([FromBody] RecordSaleCommand command, CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command, cancellationToken));
    }
}
