using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Api.Authorization;
using Sugarista.Application.Assets.CreateCafeAsset;
using Sugarista.Application.Assets.DeleteCafeAsset;
using Sugarista.Application.Assets.Dto;
using Sugarista.Application.Assets.GetCafeAssetSummary;
using Sugarista.Application.Assets.GetCafeAssets;
using Sugarista.Application.Assets.ManageAssetPhoto;
using Sugarista.Application.Assets.SetAssetStatus;
using Sugarista.Application.Assets.UpdateCafeAsset;
using Sugarista.Application.Assets.UploadAssetPhoto;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Constants;

namespace Sugarista.Api.Controllers;

/// <summary>
/// Café property registry (اموال و تجهیزات): item-by-item asset ledger with
/// count, unit price and an up-to-5-photo gallery per item.
/// </summary>
[Authorize]
[Route("api/admin/assets")]
public class AdminAssetsController : ApiControllerBase
{
    [HttpGet]
    [RequirePermission(AdminPermissions.Assets.View)]
    public async Task<IActionResult> GetAssets(
        [FromQuery] string? search = null,
        [FromQuery] string? category = null,
        [FromQuery] string? location = null,
        [FromQuery] bool? activeOnly = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null,
        [FromQuery] int page = 1,
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(new GetCafeAssetsQuery(
            search, category, location, activeOnly, sortBy, sortDirection, page),
            cancellationToken));
    }

    [HttpGet("summary")]
    [RequirePermission(AdminPermissions.Assets.View)]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new GetCafeAssetSummaryQuery(), cancellationToken));
    }

    [HttpPost]
    [RequirePermission(AdminPermissions.Assets.Create)]
    public async Task<IActionResult> CreateAsset(
        [FromBody] CreateCafeAssetCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(AdminPermissions.Assets.Update)]
    public async Task<IActionResult> UpdateAsset(
        Guid id,
        [FromBody] UpdateAssetRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new UpdateCafeAssetCommand(
            id,
            request.PersianName,
            request.EnglishName,
            request.Category,
            request.SerialNumber,
            request.Location,
            request.PurchaseDate,
            request.UnitPrice,
            request.Quantity,
            request.SupplierName,
            request.Note), cancellationToken));
    }

    /// <summary>خارج از رده کردن / بازگشت به سرویس.</summary>
    [HttpPut("{id:guid}/status")]
    [RequirePermission(AdminPermissions.Assets.Update)]
    public async Task<IActionResult> SetStatus(
        Guid id,
        [FromBody] SetAssetStatusRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(
            new SetAssetStatusCommand(id, request.IsActive, request.DisposalNote),
            cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(AdminPermissions.Assets.Delete)]
    public async Task<IActionResult> DeleteAsset(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteCafeAssetCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/photos")]
    [RequirePermission(AdminPermissions.Assets.Create)]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> UploadPhoto(
        Guid id,
        IFormFile file,
        [FromForm] string? caption,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { detail = "فایلی برای آپلود انتخاب نشده است." });
        }

        await using var stream = file.OpenReadStream();
        var upload = new StoredFileUpload(stream, file.FileName, file.ContentType, file.Length);

        return Ok(await Mediator.Send(
            new UploadAssetPhotoCommand(id, upload, caption),
            cancellationToken));
    }

    [HttpPut("photos/{photoId:guid}/primary")]
    [RequirePermission(AdminPermissions.Assets.Update)]
    public async Task<IActionResult> SetPrimaryPhoto(
        Guid photoId,
        CancellationToken cancellationToken)
    {
        await Mediator.Send(new SetPrimaryAssetPhotoCommand(photoId), cancellationToken);
        return NoContent();
    }

    [HttpDelete("photos/{photoId:guid}")]
    [RequirePermission(AdminPermissions.Assets.Delete)]
    public async Task<IActionResult> DeletePhoto(Guid photoId, CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteAssetPhotoCommand(photoId), cancellationToken);
        return NoContent();
    }
}

public record UpdateAssetRequest(
    string PersianName,
    string? EnglishName,
    string Category,
    string? SerialNumber,
    string Location,
    DateOnly? PurchaseDate,
    decimal UnitPrice,
    int Quantity,
    string? SupplierName,
    string? Note);

public record SetAssetStatusRequest(
    bool IsActive,
    string? DisposalNote);
