using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Api.Authorization;
using Sugarista.Application.Suppliers.Commands.DeleteSupplier;
using Sugarista.Application.Suppliers.Commands.UpsertSupplier;
using Sugarista.Application.Suppliers.Queries.GetSuppliers;
using Sugarista.Domain.Constants;

namespace Sugarista.Api.Controllers;

[Authorize]
[Route("api/admin/suppliers")]
public class AdminSuppliersController : ApiControllerBase
{
    [HttpGet]
    [RequireAnyPermission(AdminPermissions.Suppliers.View, AdminPermissions.Ingredients.View)]
    public async Task<ActionResult<IReadOnlyCollection<SupplierDto>>> Get(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(new GetSuppliersQuery(includeInactive), cancellationToken));
    }

    [HttpPost]
    [RequireAnyPermission(AdminPermissions.Suppliers.Create, AdminPermissions.Ingredients.Create)]
    public async Task<ActionResult<Guid>> Create([FromBody] UpsertSupplierCommand command, CancellationToken cancellationToken)
    {
        var id = await Mediator.Send(command with { Id = null }, cancellationToken);
        return Ok(id);
    }

    [HttpPut("{id:guid}")]
    [RequireAnyPermission(AdminPermissions.Suppliers.Update, AdminPermissions.Ingredients.Update)]
    public async Task<ActionResult<Guid>> Update(Guid id, [FromBody] UpsertSupplierCommand command, CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command with { Id = id }, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [RequireAnyPermission(AdminPermissions.Suppliers.Delete, AdminPermissions.Ingredients.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteSupplierCommand(id), cancellationToken);
        return NoContent();
    }
}
