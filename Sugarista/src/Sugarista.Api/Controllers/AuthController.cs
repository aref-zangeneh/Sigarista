using Microsoft.AspNetCore.Mvc;
using Sugarista.Application.Auth.Commands.LoginAdmin;

namespace Sugarista.Api.Controllers;

[Route("api/auth")]
public class AuthController : ApiControllerBase
{
    [HttpPost("login")]
    public async Task<ActionResult<AdminLoginResultDto>> Login(
        [FromBody] LoginAdminCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command, cancellationToken));
    }
}
