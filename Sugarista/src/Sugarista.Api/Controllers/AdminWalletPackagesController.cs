using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Api.Authorization;
using Sugarista.Application.WalletPackages.DeleteWalletPackage;
using Sugarista.Application.WalletPackages;
using Sugarista.Application.WalletPackages.UpsertWalletPackage;
using Sugarista.Domain.Constants;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Api.Controllers;

/// <summary>
/// Wallet charge packages: pre-defined bundle offers (e.g. charge 500.000,
/// get 50.000 bonus) the POS and online wallet panel offer as quick charges.
/// </summary>
[Authorize]
[Route("api/admin/wallet-packages")]
public class AdminWalletPackagesController : ApiControllerBase
{
    [HttpGet]
    [RequirePermission(AdminPermissions.SiteSettings.View)]
    public async Task<ActionResult<IReadOnlyCollection<AdminWalletPackageDto>>> Get(
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(new GetAdminWalletPackagesQuery(), cancellationToken));
    }

    [HttpPost]
    [RequirePermission(AdminPermissions.SiteSettings.Update)]
    public async Task<ActionResult<AdminWalletPackageDto>> Upsert(
        [FromBody] UpsertWalletPackageCommand command,
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(command, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(AdminPermissions.SiteSettings.Update)]
    public async Task<ActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await Mediator.Send(new DeleteWalletPackageCommand(id), cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
