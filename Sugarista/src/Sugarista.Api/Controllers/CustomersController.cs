using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Sugarista.Application.Customers.Queries.GetCustomerProfile;

namespace Sugarista.Api.Controllers;

/// <summary>
/// Public storefront customer endpoints. No login: the customer identifies
/// themselves by phone number (the QR menu flow) and reads their own
/// stamp-card progress, rewards and badges. Only aggregate data of the
/// identified profile is returned — not any other customer's data.
/// </summary>
[AllowAnonymous]
[EnableRateLimiting("checkout")]
[Route("api/customers")]
public class CustomersController : ApiControllerBase
{
    [HttpPost("profile")]
    public async Task<ActionResult<CustomerProfileDto?>> Profile(
        [FromBody] GetCustomerProfileQuery request,
        CancellationToken cancellationToken = default)
    {
        var result = await Mediator.Send(request, cancellationToken);
        return Ok(result);
    }
}
