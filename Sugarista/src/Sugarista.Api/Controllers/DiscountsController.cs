using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Sugarista.Application.Discounts.Engine;
using Sugarista.Application.Discounts.Queries.GetCartDiscountProgress;
using Sugarista.Application.Discounts.Queries.GetDeliverySlotDiscounts;
using Sugarista.Application.Discounts.Queries.GetDiscountPreview;

namespace Sugarista.Api.Controllers;

/// <summary>
/// Public discount endpoints used by the storefront (QR menu) to preview the
/// discount a cart would receive before the order is submitted. No order is
/// created here, and no customer data is returned — only amounts, the applied
/// discounts and the Persian rejection message for an unusable code.
/// </summary>
[AllowAnonymous]
[EnableRateLimiting("checkout")]
[Route("api/discounts")]
public class DiscountsController : ApiControllerBase
{
    [HttpPost("preview")]
    public async Task<ActionResult<DiscountEvaluationResult>> Preview(
        [FromBody] GetDiscountPreviewQuery request,
        CancellationToken cancellationToken = default)
    {
        var enriched = request with
        {
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        };

        var result = await Mediator.Send(enriched, cancellationToken);
        return Ok(result);
    }

    [HttpPost("cart-progress")]
    public async Task<ActionResult<CartDiscountProgressDto>> CartProgress(
        [FromBody] GetCartDiscountProgressQuery request,
        CancellationToken cancellationToken = default)
    {
        var result = await Mediator.Send(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("delivery-slots")]
    public async Task<ActionResult<IReadOnlyCollection<DeliverySlotDiscountDto>>> DeliverySlots(
        [FromBody] GetDeliverySlotDiscountsQuery request,
        CancellationToken cancellationToken = default)
    {
        var result = await Mediator.Send(request, cancellationToken);
        return Ok(result);
    }
}
