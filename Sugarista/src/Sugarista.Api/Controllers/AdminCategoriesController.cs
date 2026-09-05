using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Api.Authorization;
using Sugarista.Application.Categories.Commands.DeleteCategory;
using Sugarista.Application.Categories.Commands.UpsertCategory;
using Sugarista.Application.Categories.Queries.GetAdminCategories;
using Sugarista.Application.Categories.Queries.GetAdminCategoryOptions;
using Sugarista.Application.Common.Models;
using Sugarista.Domain.Constants;

namespace Sugarista.Api.Controllers;

[Authorize]
[Route("api/admin/categories")]
public class AdminCategoriesController : ApiControllerBase
{
    [HttpGet]
    [RequirePermission(AdminPermissions.Categories.View)]
    public async Task<ActionResult<PaginatedList<AdminCategoryDto>>> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(new GetAdminCategoriesQuery(page, pageSize, search), cancellationToken));
    }

    [HttpGet("options")]
    [RequireAnyPermission(
        AdminPermissions.Categories.View,
        AdminPermissions.MenuItems.View,
        AdminPermissions.MenuItems.Create,
        AdminPermissions.MenuItems.Update)]
    public async Task<ActionResult<IReadOnlyCollection<AdminCategoryOptionDto>>> GetOptions(CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new GetAdminCategoryOptionsQuery(), cancellationToken));
    }

    [HttpPost]
    [RequirePermission(AdminPermissions.Categories.Create)]
    public async Task<ActionResult<Guid>> Create([FromBody] UpsertCategoryCommand command, CancellationToken cancellationToken)
    {
        var categoryId = await Mediator.Send(command with { Id = null }, cancellationToken);
        return Ok(categoryId);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(AdminPermissions.Categories.Update)]
    public async Task<ActionResult<Guid>> Update(Guid id, [FromBody] UpsertCategoryCommand command, CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command with { Id = id }, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(AdminPermissions.Categories.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteCategoryCommand(id), cancellationToken);
        return NoContent();
    }
}
