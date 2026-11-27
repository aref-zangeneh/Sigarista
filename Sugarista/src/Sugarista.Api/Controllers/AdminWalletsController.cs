using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Api.Authorization;
using Sugarista.Application.Common.Models;
using Sugarista.Application.Customers.Commands.AdjustCustomerWallet;
using Sugarista.Application.Customers.Queries.GetAdminCustomerWallet;
using Sugarista.Application.WalletPackages;
using Sugarista.Application.WalletPackages.DeleteWalletPackage;
using Sugarista.Application.WalletPackages.UpsertWalletPackage;
using Sugarista.Application.Wallets.Commands.ApplyWalletTopUpPackage;
using Sugarista.Application.Wallets.Commands.ExpireDueCashback;
using Sugarista.Application.Wallets.Commands.UpdateWalletSettings;
using Sugarista.Application.Wallets.Queries.GetAdminWallets;
using Sugarista.Application.Wallets.Queries.GetAdminWalletSummary;
using Sugarista.Application.Wallets.Queries.GetAdminWalletTransactions;
using Sugarista.Domain.Constants;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Api.Controllers;

/// <summary>
/// Dedicated customer wallet management hub: KPIs, searchable wallet directory,
/// global transaction ledger, manual top-up/debit, top-up package execution,
/// cashback expiry sweep, and wallet rules.
/// </summary>
[Authorize]
[Route("api/admin/wallets")]
public class AdminWalletsController : ApiControllerBase
{
    [HttpGet("summary")]
    [RequirePermission(AdminPermissions.Wallets.View)]
    public async Task<ActionResult<AdminWalletSummaryDto>> GetSummary(
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(new GetAdminWalletSummaryQuery(), cancellationToken));
    }

    [HttpGet]
    [RequirePermission(AdminPermissions.Wallets.View)]
    public async Task<ActionResult<PaginatedList<AdminWalletListItemDto>>> GetWallets(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        [FromQuery] string? balanceFilter = null,
        [FromQuery] decimal? minBalance = null,
        [FromQuery] decimal? maxBalance = null,
        [FromQuery] string? sortBy = null,
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(
            new GetAdminWalletsQuery(page, pageSize, search, balanceFilter, minBalance, maxBalance, sortBy),
            cancellationToken));
    }

    [HttpGet("transactions")]
    [RequirePermission(AdminPermissions.Wallets.View)]
    public async Task<ActionResult<PaginatedList<AdminGlobalWalletTransactionDto>>> GetTransactions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30,
        [FromQuery] Guid? customerId = null,
        [FromQuery] WalletTransactionType? type = null,
        [FromQuery] string? direction = null,
        [FromQuery] string? search = null,
        [FromQuery] DateTimeOffset? fromDate = null,
        [FromQuery] DateTimeOffset? toDate = null,
        [FromQuery] decimal? minAmount = null,
        [FromQuery] decimal? maxAmount = null,
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(
            new GetAdminWalletTransactionsQuery(
                page,
                pageSize,
                customerId,
                type,
                direction,
                search,
                fromDate,
                toDate,
                minAmount,
                maxAmount),
            cancellationToken));
    }

    [HttpGet("{customerId:guid}")]
    [RequirePermission(AdminPermissions.Wallets.View)]
    public async Task<ActionResult<AdminCustomerWalletDto>> GetCustomerWallet(
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var wallet = await Mediator.Send(new GetAdminCustomerWalletQuery(customerId), cancellationToken);
        if (wallet is null)
        {
            return NotFound();
        }

        return Ok(wallet);
    }

    [HttpPost("{customerId:guid}/adjust")]
    [RequirePermission(AdminPermissions.Wallets.Update)]
    public async Task<ActionResult<AdminCustomerWalletDto>> AdjustCustomerWallet(
        Guid customerId,
        [FromBody] AdjustCustomerWalletCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.CustomerId != customerId)
        {
            return BadRequest("Route id does not match payload id.");
        }

        try
        {
            var wallet = await Mediator.Send(command, cancellationToken);
            return Ok(wallet);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (DomainException exception)
        {
            return Problem(exception.Message);
        }
    }

    [HttpPost("{customerId:guid}/topup-package")]
    [RequirePermission(AdminPermissions.Wallets.Update)]
    public async Task<ActionResult<AdminCustomerWalletDto>> ApplyTopUpPackage(
        Guid customerId,
        [FromBody] ApplyWalletTopUpPackageCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.CustomerId != customerId)
        {
            return BadRequest("Route id does not match payload id.");
        }

        try
        {
            var wallet = await Mediator.Send(command, cancellationToken);
            return Ok(wallet);
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(exception.Message);
        }
        catch (DomainException exception)
        {
            return Problem(exception.Message);
        }
    }

    [HttpPost("expire-cashback")]
    [RequirePermission(AdminPermissions.Wallets.Update)]
    public async Task<ActionResult<ExpireDueCashbackResultDto>> ExpireDueCashback(
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(new ExpireDueCashbackCommand(), cancellationToken));
    }

    [HttpPut("settings")]
    [RequirePermission(AdminPermissions.Wallets.Update)]
    public async Task<ActionResult<AdminWalletSettingsDto>> UpdateSettings(
        [FromBody] UpdateWalletSettingsCommand command,
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(command, cancellationToken));
    }

    [HttpGet("packages")]
    [RequirePermission(AdminPermissions.Wallets.View)]
    public async Task<ActionResult<IReadOnlyCollection<AdminWalletPackageDto>>> GetPackages(
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(new GetAdminWalletPackagesQuery(), cancellationToken));
    }

    [HttpPost("packages")]
    [RequirePermission(AdminPermissions.Wallets.Update)]
    public async Task<ActionResult<AdminWalletPackageDto>> UpsertPackage(
        [FromBody] UpsertWalletPackageCommand command,
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(command, cancellationToken));
    }

    [HttpDelete("packages/{id:guid}")]
    [RequirePermission(AdminPermissions.Wallets.Update)]
    public async Task<ActionResult> DeletePackage(
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
