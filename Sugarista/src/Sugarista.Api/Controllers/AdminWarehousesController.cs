using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Api.Authorization;
using Sugarista.Application.Warehouses.Commands.CreateWarehouse;
using Sugarista.Application.Warehouses.Commands.DeleteWarehouse;
using Sugarista.Application.Warehouses.Commands.TransferWarehouseStock;
using Sugarista.Application.Warehouses.Commands.UpdateWarehouse;
using Sugarista.Application.Warehouses.Dto;
using Sugarista.Application.Warehouses.Queries.GetMainWarehouseStock;
using Sugarista.Application.Warehouses.Queries.GetWarehouseStock;
using Sugarista.Application.Warehouses.Queries.GetWarehouses;
using Sugarista.Application.Warehouses.Queries.GetWarehouseTransfers;
using Sugarista.Domain.Constants;

namespace Sugarista.Api.Controllers;

/// <summary>
/// Full CRUD over admin-defined warehouses (انبار) and sub-warehouses (انبارک).
/// Reads require warehouses.view; every mutation is guarded by its own
/// permission. Deletes are rejected when the warehouse is referenced by stock
/// or transactions, so server data is never silently modified.
/// </summary>
[Authorize]
[Route("api/admin/warehouses")]
public class AdminWarehousesController : ApiControllerBase
{
    [HttpGet]
    [RequirePermission(AdminPermissions.Warehouses.View)]
    public async Task<ActionResult<IReadOnlyCollection<WarehouseDto>>> Get(
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(new GetWarehousesQuery(), cancellationToken));
    }

    /// <summary>Stock rows of the main operational inventory (انبار اصلی) for inter-warehouse transfers.</summary>
    [HttpGet("main-stock")]
    [RequireAnyPermission(AdminPermissions.Warehouses.View, AdminPermissions.Inventory.View)]
    public async Task<ActionResult<WarehouseStockResultDto>> GetMainStock(
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(new GetMainWarehouseStockQuery(), cancellationToken));
    }

    /// <summary>History of inter-warehouse transfers (تاریخچه انتقال‌های بین انبارها).</summary>
    [HttpGet("transfers")]
    [RequireAnyPermission(AdminPermissions.Warehouses.View, AdminPermissions.Inventory.View)]
    public async Task<ActionResult<IReadOnlyCollection<WarehouseTransferDto>>> GetTransfers(
        [FromQuery] Guid? warehouseId = null,
        [FromQuery] bool mainOnly = false,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(new GetWarehouseTransfersQuery(warehouseId, mainOnly, limit), cancellationToken));
    }

    /// <summary>Atomically transfers one or more ingredients between warehouses (including Main Inventory).</summary>
    [HttpPost("transfers")]
    [RequireAnyPermission(AdminPermissions.Warehouses.Update, AdminPermissions.Inventory.Update)]
    public async Task<ActionResult<WarehouseTransferDto>> TransferStock(
        [FromBody] TransferWarehouseStockCommand request,
        CancellationToken cancellationToken = default)
    {
        var result = await Mediator.Send(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>Stock rows linked to one warehouse (with ingredient info).</summary>
    [HttpGet("{id:guid}/stock")]
    [RequirePermission(AdminPermissions.Warehouses.View)]
    public async Task<ActionResult<WarehouseStockResultDto>> GetStock(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(new GetWarehouseStockQuery(id), cancellationToken));
    }

    [HttpPost]
    [RequirePermission(AdminPermissions.Warehouses.Create)]
    public async Task<ActionResult<WarehouseDto>> Create(
        [FromBody] CreateWarehouseCommand request,
        CancellationToken cancellationToken = default)
    {
        var warehouse = await Mediator.Send(request, cancellationToken);
        return CreatedAtAction(nameof(GetStock), new { id = warehouse.Id }, warehouse);
    }

    [HttpPut("{id}")]
    [RequirePermission(AdminPermissions.Warehouses.Update)]
    public async Task<ActionResult<WarehouseDto>> Update(
        Guid id,
        [FromBody] UpdateWarehouseCommand request,
        CancellationToken cancellationToken = default)
    {
        var warehouse = await Mediator.Send(request with { Id = id }, cancellationToken);
        return Ok(warehouse);
    }

    [HttpDelete("{id}")]
    [RequirePermission(AdminPermissions.Warehouses.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        await Mediator.Send(new DeleteWarehouseCommand(id), cancellationToken);
        return NoContent();
    }
}
