using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Api.Authorization;
using Sugarista.Application.Badges.Commands.DeleteBadge;
using Sugarista.Application.Badges.Commands.UpsertBadge;
using Sugarista.Application.Badges.Commands.UploadBadgeImage;
using Sugarista.Application.Badges.Dto;
using Sugarista.Application.Badges.Queries.GetAdminBadges;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Constants;

namespace Sugarista.Api.Controllers;

/// <summary>
/// Admin CRUD for the customer-club badges: name, describe, order, enable and
/// upload artwork (svg/png/jpeg). Badge earning conditions are the lifetime
/// paid-order count and/or the total paid spending.
/// </summary>
[Authorize]
[Route("api/admin/badges")]
public class AdminBadgesController : ApiControllerBase
{
    [HttpGet]
    [RequirePermission(AdminPermissions.Orders.View)]
    public async Task<ActionResult<IReadOnlyCollection<AdminBadgeDto>>> Get(CancellationToken cancellationToken = default)
    {
        var badges = await Mediator.Send(new GetAdminBadgesQuery(), cancellationToken);
        return Ok(badges);
    }

    [HttpPost]
    [RequirePermission(AdminPermissions.Orders.Update)]
    public async Task<ActionResult<AdminBadgeDto>> Create(
        [FromBody] UpsertBadgeCommand request,
        CancellationToken cancellationToken = default)
    {
        var badge = await Mediator.Send(request with { Id = Guid.Empty }, cancellationToken);
        return Ok(badge);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(AdminPermissions.Orders.Update)]
    public async Task<ActionResult<AdminBadgeDto>> Update(
        Guid id,
        [FromBody] UpsertBadgeCommand request,
        CancellationToken cancellationToken = default)
    {
        var badge = await Mediator.Send(request with { Id = id }, cancellationToken);
        return Ok(badge);
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(AdminPermissions.Orders.Update)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        await Mediator.Send(new DeleteBadgeCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("image")]
    [RequirePermission(AdminPermissions.Uploads.Create)]
    [RequestSizeLimit(8 * 1024 * 1024)]
    public async Task<ActionResult<UploadBadgeImageResultDto>> UploadImage(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
        {
            ModelState.AddModelError("file", "فایلی برای آپلود انتخاب نشده است.");
            return ValidationProblem();
        }

        await using var stream = file.OpenReadStream();
        var upload = new StoredFileUpload(stream, file.FileName, file.ContentType, file.Length);
        var imageUrl = await Mediator.Send(new UploadBadgeImageCommand(Guid.NewGuid(), upload), cancellationToken);
        return Ok(imageUrl);
    }
}
