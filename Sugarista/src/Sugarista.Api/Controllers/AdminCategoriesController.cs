using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Application.Categories.Commands.DeleteCategory;
using Sugarista.Application.Categories.Commands.UpsertCategory;
using Sugarista.Application.Categories.Queries.GetAdminCategories;
using Sugarista.Domain.Constants;

namespace Sugarista.Api.Controllers;

[Authorize(Roles = RoleNames.Admin)]
[Route("api/admin/categories")]
public class AdminCategoriesController : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<AdminCategoryDto>>> Get(CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new GetAdminCategoriesQuery(), cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<Guid>> Create([FromBody] UpsertCategoryCommand command, CancellationToken cancellationToken)
    {
        var categoryId = await Mediator.Send(command with { Id = null }, cancellationToken);
        return Ok(categoryId);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Guid>> Update(Guid id, [FromBody] UpsertCategoryCommand command, CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command with { Id = id }, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteCategoryCommand(id), cancellationToken);
        return NoContent();
    }
}
