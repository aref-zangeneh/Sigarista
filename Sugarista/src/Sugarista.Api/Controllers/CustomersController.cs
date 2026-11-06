using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Sugarista.Application.Customers.Commands.UpdateCustomerBirthday;
using Sugarista.Application.Customers.Queries.GetCustomerProfile;
using Sugarista.Domain.Exceptions;

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

    /// <summary>
    /// Phone-only self-service birth date. The birthday benefit depends on
    /// this; admins can lock customer-side changes in site settings.
    /// </summary>
    [HttpPost("profile/birthday")]
    public async Task<ActionResult> UpdateBirthday(
        [FromBody] UpdateCustomerBirthdayCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await Mediator.Send(command, cancellationToken);
            return NoContent();
        }
        catch (DomainException exception)
        {
            return Problem(exception.Message);
        }
    }
}
