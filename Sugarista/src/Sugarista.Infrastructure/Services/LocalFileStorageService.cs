using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Common.Options;

namespace Sugarista.Infrastructure.Services;

public class LocalFileStorageService : IFileStorageService
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/gif",
    };

    private readonly string _uploadRootPath;
    private readonly int _maxInventoryDocumentBytes;

    public LocalFileStorageService(IHostEnvironment environment, IOptions<FileStorageOptions> options)
    {
        var configuredRoot = options.Value.UploadRootPath;
        _uploadRootPath = Path.IsPathRooted(configuredRoot)
            ? configuredRoot
            : Path.Combine(environment.ContentRootPath, configuredRoot);
        _maxInventoryDocumentBytes = options.Value.MaxInventoryDocumentBytes;
    }

    public async Task<string> SaveInventoryDocumentAsync(StoredFileUpload file, CancellationToken cancellationToken)
    {
        if (file.Length <= 0)
        {
            throw new InvalidOperationException("Uploaded file is empty.");
        }

        if (file.Length > _maxInventoryDocumentBytes)
        {
            throw new InvalidOperationException("Uploaded file exceeds the maximum allowed size.");
        }

        if (!AllowedContentTypes.Contains(file.ContentType))
        {
            throw new InvalidOperationException("Only image files (JPEG, PNG, WebP, GIF) are allowed.");
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = file.ContentType switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                "image/webp" => ".webp",
                "image/gif" => ".gif",
                _ => ".img",
            };
        }

        extension = extension.ToLowerInvariant();
        var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".gif",
        };

        if (!allowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException("Unsupported file extension.");
        }

        var directory = Path.Combine(_uploadRootPath, "inventory-documents");
        Directory.CreateDirectory(directory);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(directory, fileName);

        await using var output = File.Create(fullPath);
        await file.Content.CopyToAsync(output, cancellationToken);

        return $"/api/files/inventory-documents/{fileName}";
    }
}
