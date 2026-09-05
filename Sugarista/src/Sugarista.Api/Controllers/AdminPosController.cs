using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Api.Authorization;
using Sugarista.Application.Pos;
using Sugarista.Application.Pos.Commands.CancelPosOrder;
using Sugarista.Application.Pos.Commands.CopyPosOrder;
using Sugarista.Application.Pos.Commands.UpsertPosOrder;
using Sugarista.Application.Pos.Queries.GetPosBootstrap;
using Sugarista.Application.Pos.Queries.GetPosOrder;
using Sugarista.Domain.Constants;

namespace Sugarista.Api.Controllers;

[Authorize]
[Route("api/admin/pos")]
public class AdminPosController : ApiControllerBase
{
    [HttpGet("bootstrap")]
    [RequirePermission(AdminPermissions.Pos.View)]
    public async Task<ActionResult<PosBootstrapDto>> GetBootstrap(CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new GetPosBootstrapQuery(), cancellationToken));
    }

    [HttpGet("orders/{id:guid}")]
    [RequirePermission(AdminPermissions.Pos.View)]
    public async Task<ActionResult<PosOrderDetailDto>> GetOrder(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new GetPosOrderQuery(id), cancellationToken));
    }

    [HttpPost("orders")]
    [RequireAnyPermission(AdminPermissions.Pos.Create, AdminPermissions.Pos.Pay, AdminPermissions.Orders.Update)]
    public async Task<ActionResult<UpsertPosOrderResultDto>> UpsertOrder(
        [FromBody] UpsertPosOrderCommand command,
        CancellationToken cancellationToken)
    {
        var adminUserId = ResolveAdminUserId();
        return Ok(await Mediator.Send(command with { ServedByAdminUserId = adminUserId }, cancellationToken));
    }

    [HttpPut("orders/{id:guid}")]
    [RequireAnyPermission(AdminPermissions.Pos.Update, AdminPermissions.Pos.Create, AdminPermissions.Pos.Pay, AdminPermissions.Orders.Update)]
    public async Task<ActionResult<UpsertPosOrderResultDto>> UpdateOrder(
        Guid id,
        [FromBody] UpsertPosOrderCommand command,
        CancellationToken cancellationToken)
    {
        var adminUserId = ResolveAdminUserId();
        return Ok(await Mediator.Send(
            command with { OrderId = id, ServedByAdminUserId = adminUserId },
            cancellationToken));
    }

    [HttpPost("orders/{id:guid}/copy")]
    [RequirePermission(AdminPermissions.Pos.Create)]
    public async Task<ActionResult<PosOrderDetailDto>> CopyOrder(Guid id, CancellationToken cancellationToken)
    {
        var adminUserId = ResolveAdminUserId();
        return Ok(await Mediator.Send(new CopyPosOrderCommand(id, adminUserId), cancellationToken));
    }

    [HttpDelete("orders/{id:guid}")]
    [RequirePermission(AdminPermissions.Pos.Delete)]
    public async Task<IActionResult> CancelOrder(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new CancelPosOrderCommand(id), cancellationToken);
        return NoContent();
    }

    private Guid? ResolveAdminUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id) ? id : null;
    }
}
