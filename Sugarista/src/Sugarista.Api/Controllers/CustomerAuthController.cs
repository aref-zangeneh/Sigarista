using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Sugarista.Application.Customers.Auth.Commands.LoginCustomer;
using Sugarista.Application.Customers.Auth.Commands.LogoutCustomer;
using Sugarista.Application.Customers.Auth.Queries.GetCustomerAuthState;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Api.Controllers;

/// <summary>
/// Phone-only customer authentication of the digital menu: the session token
/// rides an HttpOnly cookie, its lifetime comes from the site settings the
/// admin controls (e.g. 60 days = the "2 months" policy), and every validation
/// re-checks the database so revocation and deactivation take effect at once.
/// </summary>
[AllowAnonymous]
[Route("api/customers/auth")]
public class CustomerAuthController : ApiControllerBase
{
    public const string SessionCookieName = "sg_customer_session";

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<CustomerLoginResultDto>> Login(
        [FromBody] LoginCustomerCommand command,
        CancellationToken cancellationToken = default)
    {
        CustomerLoginResultDto? result;
        try
        {
            result = await Mediator.Send(command, cancellationToken);
        }
        catch (DomainException exception)
        {
            return Problem(exception.Message);
        }

        if (result is null)
        {
            return Problem("شماره موبایل معتبر نیست.", statusCode: 400);
        }

        Response.Cookies.Append(
            SessionCookieName,
            result.Token,
            new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = Request.IsHttps,
                MaxAge = TimeSpan.FromDays(result.DurationDays),
                IsEssential = true
            });

        return Ok(result);
    }

    /// <summary>Identity of the signed-in customer (null = no active session).</summary>
    [HttpGet("me")]
    [EnableRateLimiting("checkout")]
    public async Task<ActionResult<CustomerAuthStateDto?>> Me(CancellationToken cancellationToken = default)
    {
        var token = Request.Cookies[SessionCookieName];
        if (string.IsNullOrWhiteSpace(token))
        {
            return Ok(null);
        }

        var state = await Mediator.Send(new GetCustomerAuthStateQuery(token), cancellationToken);
        return Ok(state);
    }

    [HttpPost("logout")]
    [EnableRateLimiting("checkout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken = default)
    {
        var token = Request.Cookies[SessionCookieName];
        if (!string.IsNullOrWhiteSpace(token))
        {
            await Mediator.Send(new LogoutCustomerCommand(token), cancellationToken);
        }

        Response.Cookies.Delete(SessionCookieName);
        return NoContent();
    }
}
