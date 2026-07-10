using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Application.Orders.Queries.GetAdminOrders;
using Sugarista.Domain.Constants;

namespace Sugarista.Api.Controllers;

[Authorize(Roles = RoleNames.Admin)]
[Route("api/admin/orders")]
public class AdminOrdersController : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<AdminOrderListItemDto>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new GetAdminOrdersQuery(), cancellationToken));
    }
}
