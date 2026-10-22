using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Api.Authorization;
using Sugarista.Application.ActivityLogs.GetActivityLogFilters;
using Sugarista.Application.ActivityLogs.GetActivityLogs;
using Sugarista.Application.ActivityLogs.GetActivityLogSummary;
using Sugarista.Application.ActivityLogs.PurgeActivityLogs;
using Sugarista.Domain.Constants;

namespace Sugarista.Api.Controllers;

/// <summary>
/// Activity log (لاگ فعالیت‌ها): a readable, who-did-what-when timeline of every
/// action performed by admin users. Append-only, purgeable by retention policy.
/// </summary>
[Authorize]
[Route("api/admin/activity-logs")]
public class AdminActivityLogsController : ApiControllerBase
{
    [HttpGet]
    [RequirePermission(AdminPermissions.ActivityLogs.View)]
    public async Task<IActionResult> GetLogs(
        [FromQuery] string? search = null,
        [FromQuery] string? sectionId = null,
        [FromQuery] string? action = null,
        [FromQuery] string? username = null,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30,
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(
            new GetActivityLogsQuery(search, sectionId, action, username, fromDate, toDate, page, pageSize),
            cancellationToken));
    }

    [HttpGet("summary")]
    [RequirePermission(AdminPermissions.ActivityLogs.View)]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new GetActivityLogSummaryQuery(), cancellationToken));
    }

    [HttpGet("filters")]
    [RequirePermission(AdminPermissions.ActivityLogs.View)]
    public async Task<IActionResult> GetFilters(CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new GetActivityLogFiltersQuery(), cancellationToken));
    }

    /// <summary>حذف لاگ‌های قدیمی‌تر از N روز (نگهداری حجم دیتابیس).</summary>
    [HttpDelete("purge")]
    [RequirePermission(AdminPermissions.ActivityLogs.Delete)]
    public async Task<IActionResult> Purge(
        [FromQuery] int days = 90,
        CancellationToken cancellationToken = default)
    {
        var deleted = await Mediator.Send(new PurgeActivityLogsCommand(days), cancellationToken);
        return Ok(new { deleted });
    }
}
