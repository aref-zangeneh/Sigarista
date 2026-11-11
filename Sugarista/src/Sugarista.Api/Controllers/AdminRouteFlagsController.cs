using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Api.Authorization;
using Sugarista.Infrastructure.Data.DbConnection;
using Sugarista.Infrastructure.Settings;
using Sugarista.Domain.Constants;

namespace Sugarista.Api.Controllers;

/// <summary>
/// Runtime control of the site's special paths. The admin can open/close the
/// controllable ones from here; INFORMATIONAL entries describe paths owned by
/// other layers (proxy, theme) that never leave the standard posture.
/// </summary>
[Authorize]
[Route("api/admin/route-flags")]
public class AdminRouteFlagsController(
    DbConnectionManager connectionManager,
    AppSetupStore appSetupStore) : ApiControllerBase
{
    public sealed record RouteFlagDto(
        string Key,
        string Label,
        string Path,
        bool Enabled,
        bool Editable,
        string Description);

    public sealed record ToggleRequest(string Key, bool Enabled);

    [HttpGet]
    [RequirePermission(AdminPermissions.SiteSettings.View)]
    public ActionResult<IReadOnlyList<RouteFlagDto>> Get()
    {
        var secrets = appSetupStore.Load();
        var dbConfigured = connectionManager.IsConfigured;

        return Ok(new[]
        {
            new RouteFlagDto(
                "setup_page",
                "صفحهٔ راه‌اندازی اولیه (شامل تغییر رمز دیتابیس)",
                "/api/setup",
                dbConfigured && (secrets.SetupPageEnabled ?? false),
                Editable: true,
                dbConfigured
                    ? "بعد از راه‌اندازی اولیه بسته می‌ماند. فقط وقتی لازم است (مثلاً برای تغییر رمز دیتابیس) بازش کنید و بلافاصله دوباره ببندید."
                    : "تا پایان راه‌اندازی اولیه همیشه باز است و از پنل قابل بستن نیست."),
            new RouteFlagDto(
                "public_health",
                "Health از بیرون (مانیتورینگ، پینگ)",
                "/health",
                secrets.PublicHealthEnabled ?? true,
                Editable: true,
                "بستن این مسیر فقط دسترسی بیرونی را قطع میکند؛ بررسی سلامت خودِ کانتینر همیشه کار میکند و سایت را نمی‌اندازد."),

            new RouteFlagDto(
                "admin_panel",
                "پنل مدیریت",
                "/admin/",
                Enabled: true,
                Editable: false,
                "رس просмотра محافظت‌شده با رمز — باز می‌ماند."),
            new RouteFlagDto(
                "public_theme",
                "تم عمومی سایت",
                "/",
                Enabled: true,
                Editable: false,
                "منوی دیجیتال و صفحات عمومی — همیشه باز."),
            new RouteFlagDto(
                "files",
                "فایل‌های عمومی (تصاویر محصولات، فایل‌ها)",
                "/api/files",
                Enabled: true,
                Editable: false,
                "برای نمایش صفحهٔ تم لازم است — همیشه باز."),
            new RouteFlagDto(
                "db_admin",
                "Adminer (مدیریت دیتابیس)",
                "/db-admin",
                appSetupStore.IsDbAdminOpen,
                Editable: true,
                "پیش‌فرض بسته. فقط برای جلسات نگهداری بازش کنید — کار تمام شد بلافاصله ببندید. آدرس‌های بسته به صفحهٔ اصلی ریدایرکت می‌شوند.")
        });
    }

    [HttpPut]
    [RequirePermission(AdminPermissions.SiteSettings.Update)]
    public IActionResult Toggle([FromBody] ToggleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Key))
        {
            return BadRequest(new { success = false, message = "کلید مسیر ارسال نشده است." });
        }

        var secrets = appSetupStore.Load();

        switch (request.Key)
        {
            case "setup_page" when !connectionManager.IsConfigured:
                return BadRequest(new
                {
                    success = false,
                    message = "قبل از پایان راه‌اندازی اولیه نمی‌توان صفحهٔ setup را بست."
                });

            case "setup_page":
                appSetupStore.PatchRouteFlags(
                    setupPageEnabled: request.Enabled,
                    publicHealthEnabled: null);
                break;

            case "public_health":
                appSetupStore.PatchRouteFlags(
                    setupPageEnabled: null,
                    publicHealthEnabled: request.Enabled);
                break;

            case "db_admin":
                appSetupStore.SetDbAdminOpen(request.Enabled);
                break;

            default:
                return BadRequest(new
                {
                    success = false,
                    message = "این مسیر از پنل کنترل نمی‌شود."
                });
        }

        return Ok(new
        {
            success = true,
            message = request.Enabled
                ? "مسیر باز شد — بلافاصله از بیرون قابل دسترسی است."
                : "مسیر بسته شد — آدرس‌های بیرونی به صفحهٔ اصلی ریدایرکت می‌شوند؛ سایت و پنل بی‌تغییر کار میکنند."
        });
    }
}
