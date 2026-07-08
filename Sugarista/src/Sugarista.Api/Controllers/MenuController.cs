using Microsoft.AspNetCore.Mvc;
using Sugarista.Application.Menu.Queries.GetPublicMenu;

namespace Sugarista.Api.Controllers;

[Route("api/menu")]
public class MenuController : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PublicMenuDto>> Get(CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new GetPublicMenuQuery(), cancellationToken));
    }
}
