using Microsoft.AspNetCore.Mvc;
using Sugarista.Application.SiteSettings.Queries.GetSiteSettings;

namespace Sugarista.Api.Controllers;

[Route("api/site-settings")]
public class SiteSettingsController : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SiteSettingsDto>> Get(CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new GetSiteSettingsQuery(), cancellationToken));
    }
}
