using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Api.Authorization;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Constants;

namespace Sugarista.Api.Controllers;

/// <summary>
/// Admin management of the customer-club membership levels: thresholds over
/// the paid-spending evaluation window and the standing discount each level
/// grants. Customers upgrade (or fall back) automatically.
/// </summary>
[Authorize]
[Route("api/admin/membership-tiers")]
public class AdminMembershipTiersController : ApiControllerBase
{
    [HttpGet]
    [RequirePermission(AdminPermissions.Orders.View)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken = default)
    {
        var tiers = await Mediator.Send(
            new Sugarista.Application.MembershipTiers.Queries.GetAdminMembershipTiers.GetAdminMembershipTiersQuery(),
            cancellationToken);
        return Ok(tiers);
    }

    [HttpPost]
    [RequirePermission(AdminPermissions.Orders.Update)]
    public async Task<IActionResult> Create(
        [FromBody] Sugarista.Application.MembershipTiers.Commands.UpsertMembershipTier.UpsertMembershipTierCommand request,
        CancellationToken cancellationToken = default)
    {
        var tier = await Mediator.Send(request with { Id = Guid.Empty }, cancellationToken);
        return Ok(tier);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(AdminPermissions.Orders.Update)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] Sugarista.Application.MembershipTiers.Commands.UpsertMembershipTier.UpsertMembershipTierCommand request,
        CancellationToken cancellationToken = default)
    {
        var tier = await Mediator.Send(request with { Id = id }, cancellationToken);
        return Ok(tier);
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(AdminPermissions.Orders.Update)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        await Mediator.Send(
            new Sugarista.Application.MembershipTiers.Commands.DeleteMembershipTier.DeleteMembershipTierCommand(id),
            cancellationToken);
        return NoContent();
    }
}
