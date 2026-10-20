using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Api.Authorization;
using Sugarista.Application.Common.Models;
using Sugarista.Application.Exports.ExportSection;
using Sugarista.Domain.Constants;

namespace Sugarista.Api.Controllers;

/// <summary>
/// Exact Excel exports for legal authorities and backup purposes.
/// </summary>
[Authorize]
[Route("api/admin/exports")]
public class AdminExportsController : ApiControllerBase
{
    [HttpGet("{kind}")]
    [RequireAnyPermission(
        AdminPermissions.Accounting.View,
        AdminPermissions.Orders.View,
        AdminPermissions.Pos.View,
        AdminPermissions.Inventory.View,
        AdminPermissions.InventoryPurchases.View,
        AdminPermissions.Ingredients.View,
        AdminPermissions.Suppliers.View,
        AdminPermissions.MenuItems.View)]
    public async Task<IActionResult> Export(
        string kind,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        CancellationToken cancellationToken = default)
    {
        var result = await Mediator.Send(new ExportSectionQuery(kind, from, to), cancellationToken);
        return File(result.Content, result.ContentType, result.FileName);
    }
}
