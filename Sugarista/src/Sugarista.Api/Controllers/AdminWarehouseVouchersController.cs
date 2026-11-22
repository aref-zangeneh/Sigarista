using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Api.Authorization;
using Sugarista.Application.Warehouses.Commands.CancelWarehouseVoucher;
using Sugarista.Application.Warehouses.Commands.CreateWarehouseVoucher;
using Sugarista.Application.Warehouses.Dto;
using Sugarista.Application.Warehouses.Queries.GetWarehouseVoucherById;
using Sugarista.Application.Warehouses.Queries.GetWarehouseVouchers;
using Sugarista.Domain.Constants;

namespace Sugarista.Api.Controllers;

/// <summary>
/// Formal Goods Receipt Notes (رسید ورود به انبار) and Goods Issue Vouchers (سند خروج از انبار).
/// </summary>
[Authorize]
[Route("api/admin/warehouse-vouchers")]
public class AdminWarehouseVouchersController : ApiControllerBase
{
    [HttpGet]
    [RequireAnyPermission(AdminPermissions.Warehouses.View, AdminPermissions.Inventory.View)]
    public async Task<ActionResult<WarehouseVoucherListResponseDto>> GetAll(
        [FromQuery] string? type = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? warehouseId = null,
        [FromQuery] bool mainOnly = false,
        [FromQuery] string? reason = null,
        [FromQuery] string? search = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30,
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(
            new GetWarehouseVouchersQuery(type, status, warehouseId, mainOnly, reason, search, from, to, page, pageSize),
            cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [RequireAnyPermission(AdminPermissions.Warehouses.View, AdminPermissions.Inventory.View)]
    public async Task<ActionResult<WarehouseVoucherDto>> GetById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var voucher = await Mediator.Send(new GetWarehouseVoucherByIdQuery(id), cancellationToken);
        return voucher is null ? NotFound() : Ok(voucher);
    }

    [HttpPost]
    [RequireAnyPermission(
        AdminPermissions.Warehouses.Update,
        AdminPermissions.Inventory.Create,
        AdminPermissions.Inventory.Update)]
    public async Task<ActionResult<WarehouseVoucherDto>> Create(
        [FromBody] CreateWarehouseVoucherCommand request,
        CancellationToken cancellationToken = default)
    {
        var created = await Mediator.Send(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPost("{id:guid}/cancel")]
    [RequireAnyPermission(AdminPermissions.Warehouses.Update, AdminPermissions.Inventory.Update)]
    public async Task<ActionResult<WarehouseVoucherDto>> Cancel(
        Guid id,
        [FromBody] CancelWarehouseVoucherRequest request,
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(new CancelWarehouseVoucherCommand(id, request.Reason), cancellationToken));
    }
}

public record CancelWarehouseVoucherRequest(string Reason);
