using System.Text;
using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.Badges.Commands.UploadBadgeImage;

/// <summary>
/// Uploads one badge artwork (svg/png/jpeg). Raster formats go through the
/// hardened image pipeline (decode validation, metadata stripping, WebP);
/// SVG files are stored as-is after a content check that rejects scripts and
/// embedding hosts that could execute inside the storefront.
/// </summary>
public record UploadBadgeImageCommand(
    Guid BadgeId,
    StoredFileUpload File) : IRequest<UploadBadgeImageResultDto>;

public record UploadBadgeImageResultDto(string Url);

public class UploadBadgeImageCommandValidator : AbstractValidator<UploadBadgeImageCommand>
{
    public UploadBadgeImageCommandValidator()
    {
        RuleFor(command => command.File).NotNull();

        RuleFor(command => command.File!.FileName)
            .Must(fileName =>
            {
                var extension = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
                return extension is ".svg" or ".png" or ".jpg" or ".jpeg";
            })
            .WithMessage("فرمت مجاز بج: svg، png یا jpeg.")
            .When(command => command.File is not null);
    }
}

public class UploadBadgeImageCommandHandler : IRequestHandler<UploadBadgeImageCommand, UploadBadgeImageResultDto>
{
    private const int MaxSvgBytes = 512 * 1024;

    private static readonly string[] ForbiddenSvgContent =
    [
        "<script", "<foreignObject", "<embed", "<iframe", "<object", "<use ",
        "javascript:", "xlink:href=\"http", "data:text/html", "<handler"
    ];

    private readonly IImageProcessingService _imageProcessing;
    private readonly IImageFileStore _imageStore;

    public UploadBadgeImageCommandHandler(IImageProcessingService imageProcessing, IImageFileStore imageStore)
    {
        _imageProcessing = imageProcessing;
        _imageStore = imageStore;
    }

    public async Task<UploadBadgeImageResultDto> Handle(UploadBadgeImageCommand request, CancellationToken cancellationToken)
    {
        var upload = request.File
            ?? throw new DomainException("فایلی برای آپلود انتخاب نشده است.");

        await using var source = upload.Content;
        using var buffer = new MemoryStream();
        await source.CopyToAsync(buffer, cancellationToken);

        var bytes = buffer.ToArray();
        if (bytes.Length == 0)
        {
            throw new DomainException("فایل ارسال‌شده خالی است.");
        }

        var extension = Path.GetExtension(upload.FileName).ToLowerInvariant();
        if (extension is not (".svg" or ".png" or ".jpg" or ".jpeg"))
        {
            throw new DomainException("فرمت مجاز بج: svg، png یا jpeg.");
        }

        if (extension == ".svg")
        {
            var svgUrl = await SaveSvgAsync(bytes, cancellationToken);
            return new UploadBadgeImageResultDto(svgUrl);
        }

        // PNG / JPEG: the decoder is the final judge of the format — a renamed
        // SVG or anything non-raster fails here.
        using var rasterStream = new MemoryStream(bytes);
        var processed = await _imageProcessing.ProcessAsync(
            rasterStream,
            upload.FileName,
            upload.ContentType,
            cancellationToken);

        var imageUrl = await _imageStore.SaveImageAsync(
            new UploadedImageFile(processed.Data, processed.FileExtension, processed.ContentType),
            cancellationToken);

        return new UploadBadgeImageResultDto(imageUrl);
    }

    private async Task<string> SaveSvgAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        if (bytes.Length > MaxSvgBytes)
        {
            throw new DomainException($"حجم فایل SVG باید کمتر از {MaxSvgBytes / 1024} کیلوبایت باشد.");
        }

        var markup = Encoding.UTF8.GetString(bytes);
        if (!markup.Contains("<svg", StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException("فایل ارسال‌شده یک SVG معتبر نیست.");
        }

        foreach (var forbidden in ForbiddenSvgContent)
        {
            if (markup.Contains(forbidden, StringComparison.OrdinalIgnoreCase))
            {
                throw new DomainException("SVG حاوی محتوای مجاز نیست — فایل را بدون اسکریپت آپلود کنید.");
            }
        }

        return await _imageStore.SaveImageAsync(
            new UploadedImageFile(bytes, ".svg", "image/svg+xml"),
            cancellationToken);
    }
}
