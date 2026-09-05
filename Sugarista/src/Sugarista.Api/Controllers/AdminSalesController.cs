using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Api.Authorization;
using Sugarista.Application.Inventory.Commands.RecordSale;
using Sugarista.Domain.Constants;

namespace Sugarista.Api.Controllers;

[Authorize]
[Route("api/admin/sales")]
public class AdminSalesController : ApiControllerBase
{
    [HttpPost("record")]
    [RequirePermission(AdminPermissions.RecordSale.Create)]
    public async Task<ActionResult<RecordSaleResultDto>> Record([FromBody] RecordSaleCommand command, CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command, cancellationToken));
    }
}
