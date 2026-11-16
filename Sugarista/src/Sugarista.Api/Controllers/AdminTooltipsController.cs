using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Api.Authorization;
using Sugarista.Application.Tooltips.Commands.DeleteTooltipOverride;
using Sugarista.Application.Tooltips.Commands.UpsertTooltipOverride;
using Sugarista.Application.Tooltips.Dto;
using Sugarista.Application.Tooltips.Queries.GetActiveTooltipOverrides;
using Sugarista.Application.Tooltips.Queries.GetTooltipOverrides;
using Sugarista.Domain.Constants;

namespace Sugarista.Api.Controllers;

/// <summary>
/// Admin-managed tooltip overrides. The registry of available tooltip keys
/// lives in the frontend (<c>src/utils/tooltipRegistry.js</c>); this API only
/// stores the admin's custom text / active flag per key.
/// </summary>
[Authorize]
[Route("api/admin/tooltips")]
public class AdminTooltipsController : ApiControllerBase
{
    /// <summary>All stored overrides (management page).</summary>
    [HttpGet]
    [RequirePermission(AdminPermissions.Tooltips.View)]
    public async Task<ActionResult<IReadOnlyCollection<AdminTooltipOverrideDto>>> Get(
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(new GetTooltipOverridesQuery(), cancellationToken));
    }

    /// <summary>
    /// Active overrides as key → text map for the Tip component. Any
    /// authenticated admin may read it — POS cashiers don't have the tooltips
    /// permission and still need the help bubbles.
    /// </summary>
    [HttpGet("active")]
    public async Task<ActionResult<IReadOnlyDictionary<string, string>>> GetActive(
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(new GetActiveTooltipOverridesQuery(), cancellationToken));
    }

    /// <summary>Creates or updates the override for one tooltip key.</summary>
    [HttpPut("{key}")]
    [RequirePermission(AdminPermissions.Tooltips.Update)]
    public async Task<ActionResult<AdminTooltipOverrideDto>> Upsert(
        string key,
        [FromBody] UpsertTooltipOverrideCommand request,
        CancellationToken cancellationToken = default)
    {
        var tooltip = await Mediator.Send(request with { Key = key }, cancellationToken);
        return Ok(tooltip);
    }

    /// <summary>Deletes the override so the registry default is shown again.</summary>
    [HttpDelete("{key}")]
    [RequirePermission(AdminPermissions.Tooltips.Update)]
    public async Task<IActionResult> Delete(string key, CancellationToken cancellationToken = default)
    {
        await Mediator.Send(new DeleteTooltipOverrideCommand(key), cancellationToken);
        return NoContent();
    }
}
