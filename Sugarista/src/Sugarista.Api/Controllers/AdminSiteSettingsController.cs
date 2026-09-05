using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Api.Authorization;
using Sugarista.Application.SiteSettings.Commands.UpdateSiteSettings;
using Sugarista.Application.SiteSettings.Queries.GetSiteSettings;
using Sugarista.Domain.Constants;

namespace Sugarista.Api.Controllers;

[Authorize]
[Route("api/admin/site-settings")]
public class AdminSiteSettingsController : ApiControllerBase
{
    [HttpGet]
    [RequirePermission(AdminPermissions.SiteSettings.View)]
    public async Task<ActionResult<SiteSettingsDto>> Get(CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new GetSiteSettingsQuery(), cancellationToken));
    }

    [HttpPut]
    [RequirePermission(AdminPermissions.SiteSettings.Update)]
    public async Task<ActionResult<Guid>> Update(
        [FromBody] UpdateSiteSettingsCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command, cancellationToken));
    }
}
