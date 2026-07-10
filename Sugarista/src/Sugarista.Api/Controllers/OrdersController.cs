using Microsoft.AspNetCore.Mvc;
using Sugarista.Application.Orders.Commands.ConfirmMockPayment;
using Sugarista.Application.Orders.Commands.CreateOrderCheckout;
using Sugarista.Application.Orders.Queries.GetOrderByTrackingCode;

namespace Sugarista.Api.Controllers;

[Route("api/orders")]
public class OrdersController : ApiControllerBase
{
    [HttpPost("checkout")]
    public async Task<ActionResult<CreateOrderCheckoutResultDto>> Checkout(
        [FromBody] CreateOrderCheckoutCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command, cancellationToken));
    }

    [HttpPost("{id:guid}/confirm-mock")]
    public async Task<ActionResult<ConfirmMockPaymentResultDto>> ConfirmMockPayment(
        Guid id,
        [FromBody] ConfirmMockPaymentRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(
            new ConfirmMockPaymentCommand(id, request.Authority),
            cancellationToken));
    }

    [HttpGet("status/{trackingCode}")]
    public async Task<ActionResult<OrderStatusDto>> GetStatus(
        string trackingCode,
        CancellationToken cancellationToken)
    {
        var order = await Mediator.Send(new GetOrderByTrackingCodeQuery(trackingCode), cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        return Ok(order);
    }
}

public record ConfirmMockPaymentRequest(string Authority);
