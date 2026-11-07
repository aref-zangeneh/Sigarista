using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Api.Authorization;
using Sugarista.Application.Printing.Commands.UpdateReceiptLayout;
using Sugarista.Application.Printing.Queries.GetReceiptLayout;
using Sugarista.Application.Printing.ReceiptLayout;
using Sugarista.Domain.Constants;

namespace Sugarista.Api.Controllers;

/// <summary>
/// Visual receipt layout (customer copy + barista/kitchen ticket) configured
/// from the admin panel; also drives the offline print agent rendering.
/// </summary>
[Authorize]
[Route("api/admin/receipt-layout")]
public class AdminReceiptLayoutController : ApiControllerBase
{
    [HttpGet]
    [RequirePermission(AdminPermissions.SiteSettings.View)]
    public async Task<ActionResult<ResolvedReceiptLayout>> Get(CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new GetReceiptLayoutQuery(), cancellationToken));
    }

    [HttpPut]
    [RequirePermission(AdminPermissions.SiteSettings.Update)]
    public async Task<IActionResult> Update(
        [FromBody] ReceiptLayoutPatch layout,
        CancellationToken cancellationToken)
    {
        await Mediator.Send(new UpdateReceiptLayoutCommand(layout), cancellationToken);
        return NoContent();
    }
}
