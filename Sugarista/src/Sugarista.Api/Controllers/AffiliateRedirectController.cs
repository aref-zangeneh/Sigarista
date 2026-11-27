using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Affiliates.Services;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Api.Controllers;

/// <summary>
/// Public affiliate link entry point (/r/{slug}?src={subId}) with Last-Click
/// HttpOnly+Secure cookie tracking (aff_token, 14 days) and high-quality SVG/PNG QR codes.
/// </summary>
[AllowAnonymous]
public class AffiliateRedirectController : ControllerBase
{
    public const string AffiliateCookieName = "aff_token";

    private readonly IAffiliateTrackingService _trackingService;
    private readonly IAffiliateQrCodeService _qrCodeService;
    private readonly IApplicationDbContext _dbContext;
    private readonly IHostEnvironment _environment;

    public AffiliateRedirectController(
        IAffiliateTrackingService trackingService,
        IAffiliateQrCodeService qrCodeService,
        IApplicationDbContext dbContext,
        IHostEnvironment environment)
    {
        _trackingService = trackingService;
        _qrCodeService = qrCodeService;
        _dbContext = dbContext;
        _environment = environment;
    }

    [HttpGet("/r/{slug}")]
    public async Task<IActionResult> TrackAndRedirect(
        string slug,
        [FromQuery(Name = "src")] string? subId = null,
        CancellationToken cancellationToken = default)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();

        var result = await _trackingService.RecordClickAsync(
            slug,
            subId,
            ipAddress,
            userAgent,
            cancellationToken);

        if (result is null)
        {
            return Redirect("/");
        }

        // Last-Click Attribution: always overwrite any previous aff_token cookie.
        Response.Cookies.Append(
            AffiliateCookieName,
            result.TrackingToken.ToString("D"),
            new CookieOptions
            {
                HttpOnly = true,
                Secure = HttpContext.Request.IsHttps || !_environment.IsDevelopment(),
                SameSite = SameSiteMode.Lax,
                Expires = result.ExpiresAt,
                Path = "/",
                IsEssential = true,
            });

        return Redirect(result.TargetUrl);
    }

    /// <summary>
    /// Generates a crisp vector SVG (default) or high-DPI PNG QR code for an affiliate link.
    /// </summary>
    [HttpGet("/api/affiliates/links/{linkId:guid}/qr")]
    public async Task<IActionResult> GetLinkQrCode(
        Guid linkId,
        [FromQuery] string format = "svg",
        [FromQuery] int size = 512,
        CancellationToken cancellationToken = default)
    {
        var link = await _dbContext.AffiliateLinks
            .AsNoTracking()
            .Include(l => l.Affiliate)
            .FirstOrDefaultAsync(l => l.Id == linkId && l.Affiliate.Status == AffiliateStatus.Active, cancellationToken);

        if (link is null)
        {
            return NotFound("لینک بازاریاب یافت نشد.");
        }

        var origin = $"{Request.Scheme}://{Request.Host}";
        var url = string.IsNullOrWhiteSpace(link.SubId)
            ? $"{origin}/r/{Uri.EscapeDataString(link.Slug)}"
            : $"{origin}/r/{Uri.EscapeDataString(link.Slug)}?src={Uri.EscapeDataString(link.SubId)}";

        Response.Headers.CacheControl = "public, max-age=86400";

        if (string.Equals(format, "png", StringComparison.OrdinalIgnoreCase))
        {
            var pngBytes = _qrCodeService.GeneratePng(url, size);
            return File(pngBytes, "image/png", $"affiliate-{link.Slug}.png");
        }

        var svg = _qrCodeService.GenerateSvg(url, size);
        return Content(svg, "image/svg+xml; charset=utf-8");
    }
}
