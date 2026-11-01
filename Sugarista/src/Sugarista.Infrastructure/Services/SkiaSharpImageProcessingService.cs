using Microsoft.Extensions.Options;
using SkiaSharp;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Common.Options;

namespace Sugarista.Infrastructure.Services;

/// <summary>
/// Security-first image pipeline built on SkiaSharp (MIT, no license key):
/// 1. The bytes must decode as a real raster image (rejects polyglot payloads).
/// 2. Header-level dimension check first, so decompression bombs are refused
///    before a full decode.
/// 3. EXIF orientation is applied and metadata is dropped by re-encoding.
/// 4. Oversized sources are downscaled and everything that benefits is
///    encoded as WebP — visually identical images at a fraction of the weight.
/// 5. Animated images (multi-frame GIF/WebP) are kept byte-for-byte so the
///    animation is never destroyed.
/// </summary>
public class SkiaSharpImageProcessingService : IImageProcessingService
{
    private const int MaxDecodeDimension = 10_000;

    private readonly ImageProcessingOptions _options;

    public SkiaSharpImageProcessingService(IOptions<ImageProcessingOptions> options)
    {
        _options = options.Value;
    }

    public async Task<ProcessedImage> ProcessAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var originalBytes = buffer.ToArray();

        if (originalBytes.Length == 0)
        {
            throw new InvalidImageContentException("فایل تصویر خالی است.");
        }

        if (originalBytes.Length > _options.MaxUploadBytes)
        {
            throw new InvalidImageContentException(
                $"حجم تصویر بیشتر از حد مجاز ({_options.MaxUploadBytes / (1024 * 1024)} مگابایت) است.");
        }

        // ---- Step 1: cheap header probe (no full decode).
        using var probeStream = new MemoryStream(originalBytes, writable: false);
        using var codec = SKCodec.Create(probeStream);
        if (codec is null)
        {
            throw new InvalidImageContentException("فایل ارسالی یک تصویر معتبر نیست.");
        }

        if (codec.Info.Width > MaxDecodeDimension || codec.Info.Height > MaxDecodeDimension)
        {
            throw new InvalidImageContentException("ابعاد تصویر بیش از حد بزرگ است.");
        }

        // ---- Step 2: animated payloads stay untouched.
        if (codec.FrameCount > 1)
        {
            return new ProcessedImage(
                originalBytes,
                ResolveAnimatedExtension(originalBytes),
                ResolveAnimatedContentType(originalBytes),
                codec.Info.Width,
                codec.Info.Height,
                ResolveAnimatedFormatName(originalBytes),
                IsAnimated: true,
                originalBytes.Length,
                originalBytes.Length);
        }

        // ---- Step 3: full decode (decoder is the final judge of the format).
        using var decodeStream = new MemoryStream(originalBytes, writable: false);
        using var bitmap = SKBitmap.Decode(decodeStream);
        if (bitmap is null)
        {
            throw new InvalidImageContentException("فایل ارسالی یک تصویر معتبر نیست.");
        }

        var oriented = ApplyOrientation(bitmap, codec.EncodedOrigin);
        var shouldDownscale = oriented.Width > _options.MaxDimension || oriented.Height > _options.MaxDimension;
        var working = oriented;

        if (shouldDownscale)
        {
            var scale = Math.Min(
                (float)_options.MaxDimension / oriented.Width,
                (float)_options.MaxDimension / oriented.Height);
            var newWidth = Math.Max(1, (int)Math.Round(oriented.Width * scale));
            var newHeight = Math.Max(1, (int)Math.Round(oriented.Height * scale));
            var resized = oriented.Resize(new SKImageInfo(newWidth, newHeight), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
            if (resized is not null)
            {
                if (!ReferenceEquals(resized, oriented))
                {
                    oriented.Dispose();
                }
                working = resized;
            }
        }

        using (working)
            {
                using var image = SKImage.FromBitmap(working);
                var quality = Math.Clamp(_options.WebPQuality, 1, 100);

                using var webpData = image.Encode(SKEncodedImageFormat.Webp, quality);
                var webpBytes = webpData.ToArray();

                // ---- Step 4: keep the original when re-encoding does not pay off.
                if (webpBytes.Length >= originalBytes.Length && !shouldDownscale)
                {
                    if (IsJpeg(originalBytes))
                    {
                        using var jpegData = image.Encode(SKEncodedImageFormat.Jpeg, Math.Clamp(quality + 3, 1, 100));
                        var jpegBytes = jpegData.ToArray();

                        return new ProcessedImage(
                            jpegBytes,
                            ".jpg",
                            "image/jpeg",
                            working.Width,
                            working.Height,
                            "JPEG",
                            IsAnimated: false,
                            originalBytes.Length,
                            jpegBytes.Length);
                    }

                    return new ProcessedImage(
                        originalBytes,
                        ResolveSourceExtension(originalBytes),
                        ResolveSourceContentType(originalBytes),
                        working.Width,
                        working.Height,
                        ResolveSourceFormatName(originalBytes),
                        IsAnimated: false,
                        originalBytes.Length,
                        originalBytes.Length);
                }

                return new ProcessedImage(
                    webpBytes,
                    ".webp",
                    "image/webp",
                    working.Width,
                    working.Height,
                    "WebP",
                    IsAnimated: false,
                    originalBytes.Length,
                    webpBytes.Length);
            }
        }

    private static SKBitmap ApplyOrientation(SKBitmap source, SKEncodedOrigin origin)
    {
        // Clone for the common case so the caller can always dispose `source`.
        switch (origin)
        {
            case SKEncodedOrigin.TopLeft:
                return source.Copy();

            case SKEncodedOrigin.TopRight:
                return Flip(source, horizontal: true, vertical: false);

            case SKEncodedOrigin.BottomRight:
                return Rotate180(source);

            case SKEncodedOrigin.BottomLeft:
                return Flip(source, horizontal: false, vertical: true);

            case SKEncodedOrigin.LeftTop:
                return Rotate90(source, clockwise: false, mirror: true);

            case SKEncodedOrigin.RightTop:
                return Rotate90(source, clockwise: true, mirror: false);

            case SKEncodedOrigin.RightBottom:
                return Rotate90(source, clockwise: true, mirror: true);

            case SKEncodedOrigin.LeftBottom:
                return Rotate90(source, clockwise: false, mirror: false);

            default:
                return source.Copy();
        }
    }

    private static SKBitmap Flip(SKBitmap source, bool horizontal, bool vertical)
    {
        var result = new SKBitmap(source.Width, source.Height);
        using var canvas = new SKCanvas(result);
        canvas.Scale(horizontal ? -1 : 1, vertical ? -1 : 1);
        canvas.Translate(horizontal ? -source.Width : 0, vertical ? -source.Height : 0);
        canvas.DrawBitmap(source, 0, 0);
        return result;
    }

    private static SKBitmap Rotate180(SKBitmap source)
    {
        var result = new SKBitmap(source.Width, source.Height);
        using var canvas = new SKCanvas(result);
        canvas.Translate(source.Width, source.Height);
        canvas.RotateDegrees(180);
        canvas.DrawBitmap(source, 0, 0);
        return result;
    }

    private static SKBitmap Rotate90(SKBitmap source, bool clockwise, bool mirror)
    {
        var result = new SKBitmap(source.Height, source.Width);
        using var canvas = new SKCanvas(result);
        if (clockwise)
        {
            canvas.Translate(result.Width, 0);
            canvas.RotateDegrees(90);
        }
        else
        {
            canvas.Translate(0, result.Height);
            canvas.RotateDegrees(-90);
        }
        if (mirror)
        {
            canvas.Scale(-1, 1);
            canvas.Translate(-source.Width, 0);
        }
        canvas.DrawBitmap(source, 0, 0);
        return result;
    }

    private static bool IsJpeg(byte[] bytes) =>
        bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;

    private static string ResolveSourceExtension(byte[] bytes)
    {
        if (IsJpeg(bytes)) return ".jpg";
        if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47) return ".png";
        if (bytes.Length >= 4 && bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46) return ".gif";
        if (bytes.Length >= 12 && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46
            && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50) return ".webp";
        return ".img";
    }

    private static string ResolveSourceContentType(byte[] bytes)
    {
        if (IsJpeg(bytes)) return "image/jpeg";
        if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47) return "image/png";
        if (bytes.Length >= 4 && bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46) return "image/gif";
        if (bytes.Length >= 12 && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46
            && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50) return "image/webp";
        return "application/octet-stream";
    }

    private static string ResolveSourceFormatName(byte[] bytes)
    {
        if (IsJpeg(bytes)) return "JPEG";
        if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47) return "PNG";
        if (bytes.Length >= 4 && bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46) return "GIF";
        if (bytes.Length >= 12 && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46
            && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50) return "WebP";
        return "Unknown";
    }

    private static string ResolveAnimatedExtension(byte[] bytes)
    {
        if (bytes.Length >= 4 && bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46) return ".gif";
        if (bytes.Length >= 12 && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46
            && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50) return ".webp";
        return ".gif";
    }

    private static string ResolveAnimatedContentType(byte[] bytes)
    {
        if (bytes.Length >= 4 && bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46) return "image/gif";
        if (bytes.Length >= 12 && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46
            && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50) return "image/webp";
        return "image/gif";
    }

    private static string ResolveAnimatedFormatName(byte[] bytes)
    {
        if (bytes.Length >= 4 && bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46) return "GIF";
        if (bytes.Length >= 12 && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46
            && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50) return "WebP";
        return "GIF";
    }
}

/// <summary>Thrown when the incoming bytes do not decode as a supported raster image.</summary>
public class InvalidImageContentException : Exception
{
    public InvalidImageContentException(string message) : base(message) { }
}
