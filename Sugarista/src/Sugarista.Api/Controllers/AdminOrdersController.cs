using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Api.Authorization;
using Sugarista.Application.Common.Models;
using Sugarista.Application.Orders.Commands.CancelAdminOrder;
using Sugarista.Application.Orders.Commands.DeleteAdminOrder;
using Sugarista.Application.Orders.Commands.RegisterAdminPayment;
using Sugarista.Application.Orders.Commands.SetOrderAccountingExclusion;
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
        [FromQuery] string? status = null,
        [FromQuery] string? fulfillment = null,
        [FromQuery] string? orderKind = null,
        [FromQuery] bool? excludedFromAccounting = null,
        [FromQuery] string? channel = null,
        [FromQuery] string? serviceType = null,
        [FromQuery] string? paymentMethod = null,
        [FromQuery] string? fromDate = null,
        [FromQuery] string? toDate = null,
        [FromQuery] decimal? minAmount = null,
        [FromQuery] decimal? maxAmount = null,
        [FromQuery] bool? hasDiscount = null,
        [FromQuery] bool? isSplitBill = null,
        [FromQuery] bool? hasNote = null,
        [FromQuery] bool? hasManagerNote = null,
        [FromQuery] string? menuItemQuery = null,
        [FromQuery] string? tableQuery = null,
        [FromQuery] Guid? deliveryMethodId = null,
        [FromQuery] string? sortBy = null,
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(
            new GetAdminOrdersQuery(
                page,
                pageSize,
                search,
                status,
                fulfillment,
                orderKind,
                excludedFromAccounting,
                channel,
                serviceType,
                paymentMethod,
                fromDate,
                toDate,
                minAmount,
                maxAmount,
                hasDiscount,
                isSplitBill,
                hasNote,
                hasManagerNote,
                menuItemQuery,
                tableQuery,
                deliveryMethodId,
                sortBy),
            cancellationToken));
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
                request.Lines,
                request.ManagerNote),
            cancellationToken));
    }

    [HttpPost("{id:guid}/pay")]
    [RequirePermission(AdminPermissions.Orders.Update)]
    public async Task<ActionResult<AdminOrderDetailDto>> Pay(
        Guid id,
        [FromBody] RegisterAdminPaymentRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(
            new RegisterAdminPaymentCommand(
                id,
                request.PaymentMethod,
                request.Amount,
                request.OrderKind,
                request.ComplimentaryReason),
            cancellationToken));
    }

    [HttpPost("{id:guid}/cancel")]
    [RequirePermission(AdminPermissions.Orders.Delete)]
    public async Task<ActionResult<AdminOrderDetailDto>> Cancel(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new CancelAdminOrderCommand(id), cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(AdminPermissions.Orders.Delete)]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromQuery] bool restoreInventory = true,
        CancellationToken cancellationToken = default)
    {
        await Mediator.Send(new DeleteAdminOrderCommand(id, restoreInventory), cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:guid}/accounting-exclusion")]
    [RequirePermission(AdminPermissions.Orders.Update)]
    public async Task<ActionResult<AdminOrderDetailDto>> SetAccountingExclusion(
        Guid id,
        [FromBody] SetOrderAccountingExclusionRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(
            new SetOrderAccountingExclusionCommand(id, request.Exclude, request.Reason),
            cancellationToken));
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

public record SetOrderAccountingExclusionRequest(bool Exclude, string? Reason = null);

public record RegisterAdminPaymentRequest(
    string PaymentMethod = "Cash",
    decimal? Amount = null,
    string? OrderKind = null,
    string? ComplimentaryReason = null);

public record UpdateAdminOrderRequest(
    string? CustomerName,
    string? CustomerPhone,
    string? Note,
    IReadOnlyCollection<AdminOrderLineRequest>? Lines,
    string? ManagerNote = null);
