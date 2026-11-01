using Sugarista.Application.Common.Options;

namespace Sugarista.Application.Common.Interfaces;

public record UploadedImageFile(byte[] Data, string FileExtension, string ContentType);

public interface IImageProcessingService
{
    /// <summary>
    /// Validates, sanitizes and (usually) recompresses one uploaded image:
    /// the bytes must genuinely decode as a supported raster image, metadata
    /// (EXIF/GPS) is stripped, oversized edges are downscaled and the result is
    /// encoded as WebP when that actually saves bytes. Animated GIFs are kept
    /// byte-for-byte so the animation survives.
    /// </summary>
    Task<ProcessedImage> ProcessAsync(Stream content, string fileName, string contentType, CancellationToken cancellationToken = default);
}

public interface IImageFileStore
{
    /// <summary>Stores the processed image and returns its public URL (/api/files/images/{name}).</summary>
    Task<string> SaveImageAsync(UploadedImageFile image, CancellationToken cancellationToken = default);

    /// <summary>Deletes stored images by URL (safe no-op for foreign URLs).</summary>
    Task DeleteImageAsync(IEnumerable<string> imageUrls, CancellationToken cancellationToken = default);
}

public record UploadedImageResult(
    string Url,
    int Width,
    int Height,
    long SizeBytes,
    long OriginalSizeBytes,
    string Format,
    bool IsAnimated);
