using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Api.Authorization;
using Sugarista.Application.DrinkBuilder;
using Sugarista.Application.DrinkBuilder.Commands.DeleteDrinkBuilderProfile;
using Sugarista.Application.DrinkBuilder.Commands.UpdateDrinkBuilderSettings;
using Sugarista.Application.DrinkBuilder.Commands.UpsertDrinkBuilderProfile;
using Sugarista.Application.DrinkBuilder.Queries.GetDrinkBuilderProfiles;
using Sugarista.Application.DrinkBuilder.Queries.GetDrinkBuilderSettings;
using Sugarista.Domain.Constants;

namespace Sugarista.Api.Controllers;

[Authorize]
[Route("api/admin/drink-builder")]
public class AdminDrinkBuilderController : ApiControllerBase
{
    [HttpGet("profiles")]
    [RequirePermission(AdminPermissions.DrinkBuilder.View)]
    public async Task<ActionResult<IReadOnlyCollection<AdminDrinkBuilderProfileDto>>> GetProfiles(
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(new GetDrinkBuilderProfilesQuery(search), cancellationToken));
    }

    [HttpPost("profiles")]
    [RequirePermission(AdminPermissions.DrinkBuilder.Create)]
    public async Task<ActionResult<Guid>> CreateProfile(
        [FromBody] UpsertDrinkBuilderProfileCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command with { Id = null }, cancellationToken));
    }

    [HttpPut("profiles/{id:guid}")]
    [RequirePermission(AdminPermissions.DrinkBuilder.Update)]
    public async Task<ActionResult<Guid>> UpdateProfile(
        Guid id,
        [FromBody] UpsertDrinkBuilderProfileCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command with { Id = id }, cancellationToken));
    }

    [HttpDelete("profiles/{id:guid}")]
    [RequirePermission(AdminPermissions.DrinkBuilder.Delete)]
    public async Task<IActionResult> DeleteProfile(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteDrinkBuilderProfileCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpGet("settings")]
    [RequirePermission(AdminPermissions.DrinkBuilder.View)]
    public async Task<ActionResult<AdminDrinkBuilderSettingsDto>> GetSettings(CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new GetDrinkBuilderSettingsQuery(), cancellationToken));
    }

    [HttpPut("settings")]
    [RequirePermission(AdminPermissions.DrinkBuilder.Update)]
    public async Task<ActionResult<bool>> UpdateSettings(
        [FromBody] UpdateDrinkBuilderSettingsCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command, cancellationToken));
    }
}
