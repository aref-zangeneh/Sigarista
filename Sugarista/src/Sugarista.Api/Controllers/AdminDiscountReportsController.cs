using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Api.Authorization;
using Sugarista.Application.Discounts.Reports;
using Sugarista.Domain.Constants;

namespace Sugarista.Api.Controllers;

/// <summary>
/// Discount performance reports: on-screen preview, synchronous Excel download
/// and recurring e-mail schedules. Everything is read from live data, so a
/// schedule or column change applies immediately.
/// </summary>
[Authorize]
[Route("api/admin/discount-reports")]
public class AdminDiscountReportsController : ApiControllerBase
{
    [HttpGet("columns")]
    [RequirePermission(AdminPermissions.Discounts.View)]
    public ActionResult GetColumns() => Ok(DiscountReportColumns.All);

    [HttpPost("preview")]
    [RequirePermission(AdminPermissions.Discounts.View)]
    public async Task<ActionResult> Preview(
        [FromBody] GetDiscountReportQuery request,
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(request, cancellationToken));
    }

    [HttpPost("export")]
    [RequirePermission(AdminPermissions.Discounts.View)]
    public async Task<ActionResult> Export(
        [FromBody] GetDiscountReportExcelQuery request,
        CancellationToken cancellationToken = default)
    {
        var file = await Mediator.Send(request, cancellationToken);
        return File(
            file.Content,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            file.FileName);
    }

    [HttpGet("schedules")]
    [RequirePermission(AdminPermissions.Discounts.View)]
    public async Task<ActionResult> GetSchedules(CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(new GetReportSchedulesQuery(), cancellationToken));
    }

    [HttpPost("schedules")]
    [RequirePermission(AdminPermissions.Discounts.Update)]
    public async Task<ActionResult> CreateSchedule(
        [FromBody] UpsertReportScheduleCommand command,
        CancellationToken cancellationToken = default)
    {
        var id = await Mediator.Send(command with { Id = null }, cancellationToken);
        return Ok(new { id });
    }

    [HttpPut("schedules/{id:guid}")]
    [RequirePermission(AdminPermissions.Discounts.Update)]
    public async Task<ActionResult> UpdateSchedule(
        Guid id,
        [FromBody] UpsertReportScheduleCommand command,
        CancellationToken cancellationToken = default)
    {
        var savedId = await Mediator.Send(command with { Id = id }, cancellationToken);
        return Ok(new { id = savedId });
    }

    [HttpDelete("schedules/{id:guid}")]
    [RequirePermission(AdminPermissions.Discounts.Delete)]
    public async Task<ActionResult> DeleteSchedule(Guid id, CancellationToken cancellationToken = default)
    {
        await Mediator.Send(new DeleteReportScheduleCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("schedules/{id:guid}/send-now")]
    [RequirePermission(AdminPermissions.Discounts.Update)]
    public async Task<ActionResult> SendNow(Guid id, CancellationToken cancellationToken = default)
    {
        await Mediator.Send(new SendReportScheduleNowCommand(id), cancellationToken);
        return NoContent();
    }
}
