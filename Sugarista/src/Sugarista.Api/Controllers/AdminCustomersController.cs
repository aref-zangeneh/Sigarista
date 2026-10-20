using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Api.Authorization;
using Sugarista.Application.Customers.Commands.UpdateCustomer;
using Sugarista.Application.Customers.Queries.GetAdminCustomerById;
using Sugarista.Application.Customers.Queries.GetAdminCustomers;
using Sugarista.Application.Common.Models;
using Sugarista.Domain.Constants;

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
}
