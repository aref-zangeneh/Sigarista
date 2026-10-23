using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Sugarista.Application.Auth.Commands.LoginAdmin;

namespace Sugarista.Api.Controllers;

[Route("api/auth")]
[EnableRateLimiting("auth")]
public class AuthController : ApiControllerBase
{
    [HttpPost("login")]
    public async Task<ActionResult<AdminLoginResultDto>> Login(
        [FromBody] LoginAdminCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command, cancellationToken));
    }

    /// <summary>
    /// Stateless JWT: the token is discarded client-side. The call exists so the
    /// activity log can record a proper «خروج» event for the signed-in user.
    /// </summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    public IActionResult Logout() => NoContent();
}
