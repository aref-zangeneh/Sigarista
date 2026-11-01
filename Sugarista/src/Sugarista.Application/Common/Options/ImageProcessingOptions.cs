namespace Sugarista.Application.Common.Options;

/// <summary>
/// Tuning knobs for the centralized image upload pipeline (تصاویر). Everything
/// is enforced server-side; client-side limits are cosmetic only.
/// </summary>
public class ImageProcessingOptions
{
    public const string SectionName = "ImageProcessing";

    /// <summary>Hard cap on the incoming (original) upload size, in bytes.</summary>
    public int MaxUploadBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// Largest allowed edge of an image. Larger sources are downscaled to fit
    /// before encoding — a 6000px phone photo has no business on the menu.
    /// </summary>
    public int MaxDimension { get; set; } = 1920;

    /// <summary>
    /// WebP encoder quality (1-100). Chosen so menu photos stay visually
    /// identical while the payload shrinks dramatically (~5-8x vs JPEG/PNG).
    /// </summary>
    public int WebPQuality { get; set; } = 82;

    /// <summary>
    /// Sources not larger than this and already within the dimension budget are
    /// kept as-is instead of being re-encoded (re-encode would only make them
    /// bigger). Metadata is still verified and the format sniffed by decoder.
    /// </summary>
    public int SkipReencodeBelowBytes { get; set; } = 48 * 1024;
}

/// <summary>Result of the secure image pipeline.</summary>
public sealed record ProcessedImage(
    byte[] Data,
    string FileExtension,
    string ContentType,
    int Width,
    int Height,
    string FormatName,
    bool IsAnimated,
    long OriginalSizeBytes,
    long ProcessedSizeBytes);
