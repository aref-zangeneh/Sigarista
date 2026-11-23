using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Sugarista.Application.Customers.Auth.Commands.LoginCustomer;
using Sugarista.Application.Customers.Auth.Commands.LogoutCustomer;
using Sugarista.Application.Customers.Auth.Commands.RequestCustomerOtp;
using Sugarista.Application.Customers.Auth.Commands.VerifyCustomerOtp;
using Sugarista.Application.Customers.Auth.Queries.GetCustomerAuthState;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Api.Controllers;

/// <summary>
/// SMS OTP customer authentication of the digital menu: the customer enters their
/// phone number, receives a 6-digit SMS verification code, and upon verification
/// receives a persistent HttpOnly session cookie whose lifetime comes from site
/// settings (default 60 days). Every entered phone number is captured in the unified
/// customer directory and SMS contacts list.
/// </summary>
[AllowAnonymous]
[Route("api/customers/auth")]
public class CustomerAuthController : ApiControllerBase
{
    public const string SessionCookieName = "sg_customer_session";

    [HttpPost("otp/request")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<RequestCustomerOtpResultDto>> RequestOtp(
        [FromBody] RequestCustomerOtpCommand command,
        CancellationToken cancellationToken = default)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await Mediator.Send(command with { RequestIp = ip }, cancellationToken);

        if (result.Sent)
        {
            return Ok(result);
        }

        if (result.IsRateLimited)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, result);
        }

        return BadRequest(result);
    }

    [HttpPost("otp/verify")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<CustomerLoginResultDto>> VerifyOtp(
        [FromBody] VerifyCustomerOtpCommand command,
        CancellationToken cancellationToken = default)
    {
        VerifyCustomerOtpResultDto result;
        try
        {
            result = await Mediator.Send(command, cancellationToken);
        }
        catch (DomainException exception)
        {
            return Problem(exception.Message, statusCode: 400);
        }

        if (!result.Success || result.Login is null)
        {
            return Problem(result.Error ?? "کد تایید نامعتبر است.", statusCode: 400);
        }

        AppendSessionCookie(result.Login);
        return Ok(result.Login);
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public ActionResult<CustomerLoginResultDto> Login()
    {
        return Problem(
            "ورود مشتریان از طریق کد تایید پیامکی (OTP) انجام می‌شود. لطفاً صفحه را تازه‌سازی کنید.",
            statusCode: 400);
    }

    private void AppendSessionCookie(CustomerLoginResultDto login)
    {
        Response.Cookies.Append(
            SessionCookieName,
            login.Token,
            new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = Request.IsHttps,
                MaxAge = TimeSpan.FromDays(login.DurationDays),
                IsEssential = true
            });
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
