using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Api.Authorization;
using Sugarista.Application.Discounts.Security;
using Sugarista.Domain.Constants;

namespace Sugarista.Api.Controllers;

/// <summary>
/// Discount abuse protection: phone/IP blacklist and recorded code attempts.
/// Lockout thresholds themselves live in site settings and are editable live.
/// </summary>
[Authorize]
[Route("api/admin/discount-security")]
public class AdminDiscountSecurityController : ApiControllerBase
{
    [HttpGet("blacklist")]
    [RequirePermission(AdminPermissions.Discounts.View)]
    public async Task<ActionResult> GetBlacklist(CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(new GetDiscountBlacklistQuery(), cancellationToken));
    }

    [HttpPost("blacklist")]
    [RequirePermission(AdminPermissions.Discounts.Update)]
    public async Task<ActionResult> AddToBlacklist(
        [FromBody] AddDiscountBlacklistEntryCommand command,
        CancellationToken cancellationToken = default)
    {
        var id = await Mediator.Send(command, cancellationToken);
        return Ok(new { id });
    }

    [HttpPatch("blacklist/{id:guid}/active")]
    [RequirePermission(AdminPermissions.Discounts.Update)]
    public async Task<ActionResult> SetActive(
        Guid id,
        [FromBody] SetDiscountBlacklistEntryActiveRequest request,
        CancellationToken cancellationToken = default)
    {
        await Mediator.Send(new SetDiscountBlacklistEntryActiveCommand(id, request.IsActive), cancellationToken);
        return NoContent();
    }

    [HttpDelete("blacklist/{id:guid}")]
    [RequirePermission(AdminPermissions.Discounts.Update)]
    public async Task<ActionResult> DeleteFromBlacklist(Guid id, CancellationToken cancellationToken = default)
    {
        await Mediator.Send(new DeleteDiscountBlacklistEntryCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpGet("attempts")]
    [RequirePermission(AdminPermissions.Discounts.View)]
    public async Task<ActionResult> GetAttempts(
        string? identifier = null,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(new GetDiscountAttemptsQuery(identifier, take), cancellationToken));
    }

    [HttpDelete("attempts")]
    [RequirePermission(AdminPermissions.Discounts.Update)]
    public async Task<ActionResult> ClearAttempts(
        string? identifier = null,
        CancellationToken cancellationToken = default)
    {
        var removed = await Mediator.Send(new ClearDiscountAttemptsCommand(identifier), cancellationToken);
        return Ok(new { removed });
    }
}

public record SetDiscountBlacklistEntryActiveRequest(bool IsActive);
