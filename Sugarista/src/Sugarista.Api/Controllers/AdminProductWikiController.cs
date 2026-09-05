using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Api.Authorization;
using Sugarista.Application.ProductWiki.Commands.UpsertProductWiki;
using Sugarista.Application.ProductWiki.Queries.GetProductWiki;
using Sugarista.Domain.Constants;

namespace Sugarista.Api.Controllers;

[Authorize]
[Route("api/admin/product-wiki")]
public class AdminProductWikiController : ApiControllerBase
{
    [HttpGet("{menuItemId:guid}")]
    [RequirePermission(AdminPermissions.ProductWiki.View)]
    public async Task<ActionResult<ProductWikiDto>> Get(Guid menuItemId, CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new GetProductWikiQuery(menuItemId), cancellationToken));
    }

    [HttpPut("{menuItemId:guid}")]
    [RequirePermission(AdminPermissions.ProductWiki.Update)]
    public async Task<ActionResult<Guid>> Upsert(
        Guid menuItemId,
        [FromBody] UpsertProductWikiCommand command,
        CancellationToken cancellationToken)
    {
        if (menuItemId != command.MenuItemId)
        {
            return BadRequest("Menu item id mismatch.");
        }

        var guideId = await Mediator.Send(command, cancellationToken);
        return Ok(guideId);
    }
}
