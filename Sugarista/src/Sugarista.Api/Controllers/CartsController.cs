using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Sugarista.Application.Carts.Commands.TrackCart;

namespace Sugarista.Api.Controllers;

/// <summary>
/// Anonymous storefront cart heartbeat. The browser keeps the real cart in
/// localStorage; this endpoint mirrors it on the server so the abandoned-cart
/// reminder sweep can send a comeback code when the cart stays unpaid.
/// </summary>
[AllowAnonymous]
[EnableRateLimiting("checkout")]
[Route("api/carts")]
public class CartsController : ApiControllerBase
{
    [HttpPost("track")]
    public async Task<ActionResult<TrackCartResultDto>> Track(
        [FromBody] TrackCartCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command, cancellationToken));
    }
}
