using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Common.Options;

namespace Sugarista.Infrastructure.Services;

/// <summary>
/// Disk-backed store for processed site images (uploads/images). URLs stay
/// relative (/api/files/images/{name}) so they work behind any host/proxy.
/// </summary>
public class LocalImageFileStore : IImageFileStore
{
    private const string Bucket = "images";
    private const string UrlPrefix = "/api/files/images/";

    private readonly string _uploadRootPath;

    public LocalImageFileStore(IHostEnvironment environment, IOptions<FileStorageOptions> options)
    {
        var configuredRoot = options.Value.UploadRootPath;
        _uploadRootPath = Path.IsPathRooted(configuredRoot)
            ? configuredRoot
            : Path.Combine(environment.ContentRootPath, configuredRoot);
    }

    public async Task<string> SaveImageAsync(UploadedImageFile image, CancellationToken cancellationToken = default)
    {
        var directory = Path.Combine(_uploadRootPath, Bucket);
        Directory.CreateDirectory(directory);

        var fileName = $"{Guid.NewGuid():N}{image.FileExtension}";
        var fullPath = Path.GetFullPath(Path.Combine(directory, fileName));

        await File.WriteAllBytesAsync(fullPath, image.Data, cancellationToken);

        return $"{UrlPrefix}{fileName}";
    }

    public async Task DeleteImageAsync(IEnumerable<string> imageUrls, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetFullPath(Path.Combine(_uploadRootPath, Bucket));

        foreach (var url in imageUrls)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(url) ||
                !url.StartsWith(UrlPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var fileName = Path.GetFileName(url[UrlPrefix.Length..]);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                continue;
            }

            var fullPath = Path.GetFullPath(Path.Combine(directory, fileName));
            if (!fullPath.StartsWith(directory, StringComparison.OrdinalIgnoreCase))
            {
                continue; // path traversal guard — never touch anything outside images/
            }

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }

            await Task.CompletedTask;
        }
    }
}
