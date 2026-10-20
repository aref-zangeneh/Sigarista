using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Api.Authorization;
using Sugarista.Application.Common.Models;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Exports.CreateExportJob;
using Sugarista.Application.Exports.DeleteExportJob;
using Sugarista.Application.Exports.GetExportJobFile;
using Sugarista.Application.Exports.GetExportJobs;
using Sugarista.Domain.Constants;
using Sugarista.Domain.Entities;

namespace Sugarista.Api.Controllers;

/// <summary>
/// Async-export center. Jobs are created (queued), processed by the background
/// runner and downloaded as streamed files — large datasets never hold or time
/// out an HTTP request.
/// </summary>
[Authorize]
[Route("api/admin/exports")]
public class AdminExportsController : ApiControllerBase
{
    [HttpPost("jobs")]
    [RequireAnyPermission(
        AdminPermissions.Accounting.View,
        AdminPermissions.Orders.View,
        AdminPermissions.Pos.View,
        AdminPermissions.Inventory.View,
        AdminPermissions.InventoryPurchases.View,
        AdminPermissions.Ingredients.View,
        AdminPermissions.Suppliers.View,
        AdminPermissions.MenuItems.View)]
    public async Task<IActionResult> CreateJob(
        [FromBody] CreateExportJobCommand request,
        CancellationToken cancellationToken)
    {
        var job = await Mediator.Send(request, cancellationToken);
        return Ok(job);
    }

    [HttpGet("jobs")]
    [RequireAnyPermission(
        AdminPermissions.Accounting.View,
        AdminPermissions.Orders.View,
        AdminPermissions.Pos.View,
        AdminPermissions.Inventory.View,
        AdminPermissions.InventoryPurchases.View,
        AdminPermissions.Ingredients.View,
        AdminPermissions.Suppliers.View,
        AdminPermissions.MenuItems.View)]
    public async Task<IActionResult> GetJobs(
        [FromQuery] int page = 1,
        CancellationToken cancellationToken = default)
    {
        var jobs = await Mediator.Send(new GetExportJobsQuery(page), cancellationToken);
        return Ok(jobs);
    }

    [HttpGet("jobs/{id:guid}/download")]
    [RequireAnyPermission(
        AdminPermissions.Accounting.View,
        AdminPermissions.Orders.View,
        AdminPermissions.Pos.View,
        AdminPermissions.Inventory.View,
        AdminPermissions.InventoryPurchases.View,
        AdminPermissions.Ingredients.View,
        AdminPermissions.Suppliers.View,
        AdminPermissions.MenuItems.View)]
    public async Task<IActionResult> Download(
        Guid id,
        CancellationToken cancellationToken)
    {
        var file = await Mediator.Send(new GetExportJobFileQuery(id), cancellationToken);

        var fileStore = HttpContext.RequestServices.GetRequiredService<IExportFileStore>();
        var stream = fileStore.OpenRead(file.StoragePath);
        return File(
            stream,
            file.ContentType,
            file.FileName,
            enableRangeProcessing: false);
    }

    [HttpDelete("jobs/{id:guid}")]
    [RequireAnyPermission(
        AdminPermissions.Accounting.View,
        AdminPermissions.Orders.View,
        AdminPermissions.Pos.View,
        AdminPermissions.Inventory.View,
        AdminPermissions.InventoryPurchases.View,
        AdminPermissions.Ingredients.View,
        AdminPermissions.Suppliers.View,
        AdminPermissions.MenuItems.View)]
    public async Task<IActionResult> DeleteJob(
        Guid id,
        CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteExportJobCommand(id), cancellationToken);
        return NoContent();
    }
}
