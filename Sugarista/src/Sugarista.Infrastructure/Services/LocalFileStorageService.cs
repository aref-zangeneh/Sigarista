using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Common.Options;

namespace Sugarista.Infrastructure.Services;

public class LocalFileStorageService : IFileStorageService, IExportFileStore
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

    /// <summary>Saves a photo of a café asset into uploads/asset-photos.</summary>
    public async Task<string> SaveAssetPhotoAsync(StoredFileUpload file, CancellationToken cancellationToken = default)
    {
        if (file.Length <= 0)
        {
            throw new InvalidOperationException("Uploaded file is empty.");
        }

        if (file.Length > _maxInventoryDocumentBytes)
        {
            throw new InvalidOperationException("حجم تصویر بیشتر از حد مجاز (۵ مگابایت) است.");
        }

        if (!AllowedContentTypes.Contains(file.ContentType))
        {
            throw new InvalidOperationException("فقط تصاویر JPEG، PNG، WebP و GIF پذیرفته می‌شوند.");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".gif",
        };

        if (!allowedExtensions.Contains(extension))
        {
            extension = file.ContentType switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                "image/webp" => ".webp",
                "image/gif" => ".gif",
                _ => ".jpg",
            };
        }

        var directory = Path.Combine(_uploadRootPath, "asset-photos");
        Directory.CreateDirectory(directory);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(directory, fileName);

        await using var output = File.Create(fullPath);
        await file.Content.CopyToAsync(output, cancellationToken);

        return $"/api/files/asset-photos/{fileName}";
    }

    /// <summary>Deletes a stored asset photo file. Ignores URLs that do not belong to the asset gallery.</summary>
    public Task DeleteAssetPhotoAsync(string imageUrl, CancellationToken cancellationToken = default)
    {
        const string prefix = "/api/files/asset-photos/";

        if (string.IsNullOrWhiteSpace(imageUrl) ||
            !imageUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        var fileName = Path.GetFileName(imageUrl[prefix.Length..]);

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return Task.CompletedTask;
        }

        var directory = Path.GetFullPath(Path.Combine(_uploadRootPath, "asset-photos"));
        var fullPath = Path.GetFullPath(Path.Combine(directory, fileName));

        if (!fullPath.StartsWith(directory, StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    public async Task<string> SaveAsync(string fileName, byte[] content, CancellationToken cancellationToken = default)
    {
        if (content is { Length: <= 0 })
        {
            throw new InvalidOperationException("Export file content is empty.");
        }

        var directory = Path.Combine(_uploadRootPath, "exports");
        Directory.CreateDirectory(directory);

        var safeName = $"{Path.GetFileNameWithoutExtension(fileName)}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(4))}{Path.GetExtension(fileName)}";
        var fullPath = Path.Combine(directory, safeName);

        await File.WriteAllBytesAsync(fullPath, content, cancellationToken);
        return fullPath;
    }

    public Stream OpenRead(string storagePath)
    {
        var fullPath = ResolveSafePath(storagePath);
        return File.OpenRead(fullPath);
    }

    public void Delete(string storagePath)
    {
        var fullPath = ResolveSafePath(storagePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }

    private string ResolveSafePath(string storagePath)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            throw new ArgumentException("Export storage path is empty.");
        }

        var fullPath = Path.IsPathRooted(storagePath)
            ? storagePath
            : Path.Combine(_uploadRootPath, storagePath);

        var normalizedRoot = Path.GetFullPath(Path.Combine(_uploadRootPath, "exports"));
        var normalized = Path.GetFullPath(fullPath);

        if (!normalized.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Export storage path escapes the exports directory.");
        }

        return normalized;
    }
}
