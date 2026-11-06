using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Api.Authorization;
using Sugarista.Application.Customers.Commands.AdjustCustomerWallet;
using Sugarista.Application.Customers.Commands.UpdateCustomer;
using Sugarista.Application.Customers.Queries.GetAdminCustomerById;
using Sugarista.Application.Customers.Queries.GetAdminCustomerWallet;
using Sugarista.Application.Customers.Queries.GetAdminCustomers;
using Sugarista.Application.Common.Models;
using Sugarista.Domain.Constants;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Api.Controllers;

[Authorize]
[Route("api/admin/customers")]
public class AdminCustomersController : ApiControllerBase
{
    [HttpGet]
    [RequirePermission(AdminPermissions.Orders.View)]
    public async Task<ActionResult<PaginatedList<AdminCustomerListItemDto>>> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(new GetAdminCustomersQuery(page, pageSize, search), cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(AdminPermissions.Orders.View)]
    public async Task<ActionResult<AdminCustomerDetailDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var customer = await Mediator.Send(new GetAdminCustomerByIdQuery(id), cancellationToken);
        if (customer is null)
        {
            return NotFound();
        }

        return Ok(customer);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(AdminPermissions.Orders.Update)]
    public async Task<ActionResult> Update(
        Guid id,
        [FromBody] UpdateCustomerCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Id != id)
        {
            return BadRequest("Route id does not match payload id.");
        }
        await Mediator.Send(command, cancellationToken);
        return NoContent();
    }

    /// <summary>Wallet balance + the full logged transaction history (last 50).</summary>
    [HttpGet("{id:guid}/wallet")]
    [RequirePermission(AdminPermissions.Orders.View)]
    public async Task<ActionResult<AdminCustomerWalletDto>> GetWallet(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var wallet = await Mediator.Send(new GetAdminCustomerWalletQuery(id), cancellationToken);
        if (wallet is null)
        {
            return NotFound();
        }

        return Ok(wallet);
    }

    /// <summary>
    /// Charges the wallet (positive amount, issued against cash brought to the
    /// counter) or applies a manual correction (negative). Balance can never
    /// go below zero; every action is logged as one wallet transaction.
    /// </summary>
    [HttpPost("{id:guid}/wallet")]
    [RequirePermission(AdminPermissions.Orders.Update)]
    public async Task<ActionResult<AdminCustomerWalletDto>> AdjustWallet(
        Guid id,
        [FromBody] AdjustCustomerWalletCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.CustomerId != id)
        {
            return BadRequest("Route id does not match payload id.");
        }

        try
        {
            var wallet = await Mediator.Send(command, cancellationToken);
            return Ok(wallet);
        }
        catch (DomainException exception)
        {
            return Problem(exception.Message);
        }
    }
}
