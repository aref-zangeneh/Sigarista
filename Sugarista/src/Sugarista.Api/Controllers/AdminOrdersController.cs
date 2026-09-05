using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Api.Authorization;
using Sugarista.Application.Common.Models;
using Sugarista.Application.Orders.Commands.CancelAdminOrder;
using Sugarista.Application.Orders.Commands.UpdateAdminOrder;
using Sugarista.Application.Orders.Commands.UpdateOrderFulfillment;
using Sugarista.Application.Orders.Queries.GetAdminOrderAlerts;
using Sugarista.Application.Orders.Queries.GetAdminOrderById;
using Sugarista.Application.Orders.Queries.GetAdminOrders;
using Sugarista.Domain.Constants;
using Sugarista.Domain.Entities;

namespace Sugarista.Api.Controllers;

[Authorize]
[Route("api/admin/orders")]
public class AdminOrdersController : ApiControllerBase
{
    [HttpGet("alerts")]
    [RequirePermission(AdminPermissions.Orders.View)]
    public async Task<ActionResult<AdminOrderAlertsDto>> GetAlerts(
        [FromQuery] DateTimeOffset? since = null,
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(new GetAdminOrderAlertsQuery(since), cancellationToken));
    }

    [HttpGet]
    [RequirePermission(AdminPermissions.Orders.View)]
    public async Task<ActionResult<PaginatedList<AdminOrderListItemDto>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(new GetAdminOrdersQuery(page, pageSize, search), cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(AdminPermissions.Orders.View)]
    public async Task<ActionResult<AdminOrderDetailDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var order = await Mediator.Send(new GetAdminOrderByIdQuery(id), cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        return Ok(order);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(AdminPermissions.Orders.Update)]
    public async Task<ActionResult<AdminOrderDetailDto>> Update(
        Guid id,
        [FromBody] UpdateAdminOrderRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(
            new UpdateAdminOrderCommand(
                id,
                request.CustomerName,
                request.CustomerPhone,
                request.Note,
                request.Lines),
            cancellationToken));
    }

    [HttpPost("{id:guid}/cancel")]
    [RequirePermission(AdminPermissions.Orders.Delete)]
    public async Task<ActionResult<AdminOrderDetailDto>> Cancel(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new CancelAdminOrderCommand(id), cancellationToken));
    }

    [HttpPatch("{id:guid}/fulfillment")]
    [RequirePermission(AdminPermissions.Orders.Update)]
    public async Task<ActionResult<AdminOrderDetailDto>> UpdateFulfillment(
        Guid id,
        [FromBody] UpdateOrderFulfillmentRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(
            new UpdateOrderFulfillmentCommand(id, request.FulfillmentStatus),
            cancellationToken));
    }
}

public record UpdateOrderFulfillmentRequest(FulfillmentStatus FulfillmentStatus);

public record UpdateAdminOrderRequest(
    string? CustomerName,
    string? CustomerPhone,
    string? Note,
    IReadOnlyCollection<AdminOrderLineRequest>? Lines);
