using Microsoft.AspNetCore.Mvc;
using Sugarista.Application.Health.Queries.GetHealth;

namespace Sugarista.Api.Controllers;

[Route("api/health")]
public class HealthController : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<HealthDto>> Get(CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new GetHealthQuery(), cancellationToken));
    }
}
