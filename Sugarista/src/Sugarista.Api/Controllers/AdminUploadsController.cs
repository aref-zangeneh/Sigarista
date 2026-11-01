using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Sugarista.Api.Authorization;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Constants;
using Sugarista.Infrastructure.Services;

namespace Sugarista.Api.Controllers;

[Authorize]
[Route("api/admin/uploads")]
[EnableRateLimiting("uploads")]
public class AdminUploadsController : ApiControllerBase
{
    [HttpPost("inventory-documents")]
    [RequirePermission(AdminPermissions.Uploads.Create)]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<ActionResult<UploadedFileDto>> UploadInventoryDocument(
        IFormFile file,
        [FromServices] IFileStorageService fileStorage,
        CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        var upload = new StoredFileUpload(stream, file.FileName, file.ContentType, file.Length);
        var url = await fileStorage.SaveInventoryDocumentAsync(upload, cancellationToken);
        return Ok(new UploadedFileDto(url));
    }

    /// <summary>
    /// Centralized, hardened image upload (تصاویر سایت): decoder-validated
    /// content, metadata stripped, auto-compressed to WebP — animated images
    /// pass through untouched. Used by menu items, galleries and every other
    /// place the site shows a picture.
    /// </summary>
    [HttpPost("images")]
    [RequirePermission(AdminPermissions.Uploads.Create)]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<UploadedImageResult>> UploadImage(
        IFormFile file,
        [FromServices] IImageProcessingService imageProcessing,
        [FromServices] IImageFileStore imageStore,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            ModelState.AddModelError("file", "تصویری برای آپلود انتخاب نشده است.");
            return ValidationProblem();
        }

        try
        {
            await using var stream = file.OpenReadStream();

            var processed = await imageProcessing.ProcessAsync(
                stream, file.FileName, file.ContentType, cancellationToken);

            var url = await imageStore.SaveImageAsync(
                new UploadedImageFile(processed.Data, processed.FileExtension, processed.ContentType),
                cancellationToken);

            return Ok(new UploadedImageResult(
                url,
                processed.Width,
                processed.Height,
                processed.ProcessedSizeBytes,
                processed.OriginalSizeBytes,
                processed.FormatName,
                processed.IsAnimated));
        }
        catch (InvalidImageContentException exception)
        {
            ModelState.AddModelError("file", exception.Message);
            return ValidationProblem();
        }
    }
}

public record UploadedFileDto(string Url);

public record UploadedImageResult(
    string Url,
    int Width,
    int Height,
    long SizeBytes,
    long OriginalSizeBytes,
    string Format,
    bool IsAnimated);
