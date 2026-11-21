using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Api.Authorization;
using Sugarista.Application.OptionTemplates;
using Sugarista.Application.OptionTemplates.Commands.ApplyOptionGroupTemplate;
using Sugarista.Application.OptionTemplates.Commands.DeleteOptionGroupTemplate;
using Sugarista.Application.OptionTemplates.Commands.DuplicateOptionGroupTemplate;
using Sugarista.Application.OptionTemplates.Commands.UpsertOptionGroupTemplate;
using Sugarista.Application.OptionTemplates.Queries.GetOptionGroupTemplates;
using Sugarista.Domain.Constants;

namespace Sugarista.Api.Controllers;

[Authorize]
[Route("api/admin/option-templates")]
public class AdminOptionTemplatesController : ApiControllerBase
{
    [HttpGet]
    [RequirePermission(AdminPermissions.MenuItems.View)]
    public async Task<ActionResult<OptionGroupTemplatesResponseDto>> Get(CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new GetOptionGroupTemplatesQuery(), cancellationToken));
    }

    [HttpPost]
    [RequirePermission(AdminPermissions.MenuItems.Create)]
    public async Task<ActionResult<Guid>> Create(
        [FromBody] UpsertOptionGroupTemplateCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command with { Id = null }, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(AdminPermissions.MenuItems.Update)]
    public async Task<ActionResult<Guid>> Update(
        Guid id,
        [FromBody] UpsertOptionGroupTemplateCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command with { Id = id }, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(AdminPermissions.MenuItems.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteOptionGroupTemplateCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/duplicate")]
    [RequirePermission(AdminPermissions.MenuItems.Create)]
    public async Task<ActionResult<Guid>> Duplicate(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new DuplicateOptionGroupTemplateCommand(id), cancellationToken));
    }

    [HttpPost("{id:guid}/apply")]
    [RequirePermission(AdminPermissions.MenuItems.Update)]
    public async Task<ActionResult<ApplyOptionGroupTemplateResultDto>> Apply(
        Guid id,
        [FromBody] ApplyOptionGroupTemplateCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command with { TemplateId = id }, cancellationToken));
    }
}
