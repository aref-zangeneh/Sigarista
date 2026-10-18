using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Api.Authorization;
using Sugarista.Application.DeliveryMethods.Commands.DeleteDeliveryMethod;
using Sugarista.Application.DeliveryMethods.Commands.UpsertDeliveryMethod;
using Sugarista.Application.DeliveryMethods.Queries.GetDeliveryMethodOptions;
using Sugarista.Application.DeliveryMethods.Queries.GetDeliveryMethods;
using Sugarista.Domain.Constants;

namespace Sugarista.Api.Controllers;

[Authorize]
[Route("api/admin/delivery-methods")]
public class AdminDeliveryMethodsController : ApiControllerBase
{
    [HttpGet]
    [RequirePermission(AdminPermissions.Orders.View)]
    public async Task<ActionResult<IReadOnlyCollection<DeliveryMethodDto>>> Get(
        [FromQuery] bool includeInactive = true,
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(new GetDeliveryMethodsQuery(includeInactive), cancellationToken));
    }

    [HttpGet("options")]
    [RequireAnyPermission(AdminPermissions.Orders.View, AdminPermissions.Pos.View, AdminPermissions.Pos.Pay, AdminPermissions.Pos.Create)]
    public async Task<ActionResult<IReadOnlyCollection<DeliveryMethodOptionDto>>> GetOptions(
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(new GetDeliveryMethodOptionsQuery(), cancellationToken));
    }

    [HttpPost]
    [RequirePermission(AdminPermissions.Orders.Update)]
    public async Task<ActionResult<Guid>> Create(
        [FromBody] UpsertDeliveryMethodCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(AdminPermissions.Orders.Update)]
    public async Task<ActionResult<Guid>> Update(
        Guid id,
        [FromBody] UpsertDeliveryMethodCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Id != id)
        {
            return BadRequest("Route id does not match payload id.");
        }

        return Ok(await Mediator.Send(command, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(AdminPermissions.Orders.Delete)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteDeliveryMethodCommand(id), cancellationToken);
        return NoContent();
    }
}
