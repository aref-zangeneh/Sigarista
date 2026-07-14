using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Constants;

namespace Sugarista.Api.Controllers;

[Authorize(Roles = RoleNames.Admin)]
[Route("api/admin/uploads")]
public class AdminUploadsController : ApiControllerBase
{
    [HttpPost("inventory-documents")]
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
}

public record UploadedFileDto(string Url);
