using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Api.Authorization;
using Sugarista.Application.Maintenance;
using Sugarista.Application.Maintenance.Commands.CreateMaintenanceServiceLog;
using Sugarista.Application.Maintenance.Commands.DeleteMaintenanceEquipment;
using Sugarista.Application.Maintenance.Commands.DeleteMaintenanceSchedule;
using Sugarista.Application.Maintenance.Commands.DeleteMaintenanceServiceLog;
using Sugarista.Application.Maintenance.Commands.ReplaceMaintenanceDefaultRecipients;
using Sugarista.Application.Maintenance.Commands.TestMaintenanceReminder;
using Sugarista.Application.Maintenance.Commands.UpsertMaintenanceEquipment;
using Sugarista.Application.Maintenance.Commands.UpsertMaintenanceSchedule;
using Sugarista.Application.Maintenance.Queries.GetMaintenanceDefaultRecipients;
using Sugarista.Application.Maintenance.Queries.GetMaintenanceEquipments;
using Sugarista.Application.Maintenance.Queries.GetMaintenanceServiceLogs;
using Sugarista.Domain.Constants;

namespace Sugarista.Api.Controllers;

/// <summary>
/// Admin surface for the «سرویس دوره‌ای تجهیزات» module: equipment registry,
/// recurring schedules with dynamic SMS/email recipients, service/repair cost
/// logs and manual test reminders.
/// </summary>
[Authorize]
[Route("api/admin/maintenance")]
public class AdminMaintenanceController : ApiControllerBase
{
    // ── Equipment ──
    [HttpGet("equipments")]
    [RequirePermission(AdminPermissions.Maintenance.View)]
    public async Task<ActionResult<IReadOnlyList<MaintenanceEquipmentDto>>> GetEquipments(
        [FromQuery] bool includeInactive = true,
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(new GetMaintenanceEquipmentsQuery(includeInactive), cancellationToken));
    }

    [HttpPost("equipments")]
    [RequirePermission(AdminPermissions.Maintenance.Create)]
    public async Task<ActionResult<Guid>> CreateEquipment(
        [FromBody] UpsertMaintenanceEquipmentCommand command,
        CancellationToken cancellationToken)
    {
        var id = await Mediator.Send(command with { Id = null }, cancellationToken);
        return Ok(id);
    }

    [HttpPut("equipments/{id:guid}")]
    [RequirePermission(AdminPermissions.Maintenance.Update)]
    public async Task<ActionResult<Guid>> UpdateEquipment(
        Guid id,
        [FromBody] UpsertMaintenanceEquipmentCommand command,
        CancellationToken cancellationToken)
    {
        var updatedId = await Mediator.Send(command with { Id = id }, cancellationToken);
        return Ok(updatedId);
    }

    [HttpDelete("equipments/{id:guid}")]
    [RequirePermission(AdminPermissions.Maintenance.Delete)]
    public async Task<IActionResult> DeleteEquipment(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteMaintenanceEquipmentCommand(id), cancellationToken);
        return NoContent();
    }

    // ── Schedules (periodic reminders + dynamic recipients) ──
    [HttpPost("equipments/{equipmentId:guid}/schedules")]
    [RequirePermission(AdminPermissions.Maintenance.Create)]
    public async Task<ActionResult<Guid>> CreateSchedule(
        Guid equipmentId,
        [FromBody] UpsertMaintenanceScheduleCommand command,
        CancellationToken cancellationToken)
    {
        var id = await Mediator.Send(command with
        {
            EquipmentId = equipmentId,
            Id = null,
        }, cancellationToken);
        return Ok(id);
    }

    [HttpPut("equipments/{equipmentId:guid}/schedules/{id:guid}")]
    [RequirePermission(AdminPermissions.Maintenance.Update)]
    public async Task<ActionResult<Guid>> UpdateSchedule(
        Guid equipmentId,
        Guid id,
        [FromBody] UpsertMaintenanceScheduleCommand command,
        CancellationToken cancellationToken)
    {
        var updatedId = await Mediator.Send(command with
        {
            EquipmentId = equipmentId,
            Id = id,
        }, cancellationToken);
        return Ok(updatedId);
    }

    [HttpDelete("equipments/{equipmentId:guid}/schedules/{id:guid}")]
    [RequirePermission(AdminPermissions.Maintenance.Delete)]
    public async Task<IActionResult> DeleteSchedule(
        Guid equipmentId,
        Guid id,
        CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteMaintenanceScheduleCommand(equipmentId, id), cancellationToken);
        return NoContent();
    }

    /// <summary>Sends an immediate test SMS/email to every recipient of a schedule (no cycle rolling).</summary>
    [HttpPost("schedules/{scheduleId:guid}/test-reminder")]
    [RequirePermission(AdminPermissions.Maintenance.Update)]
    public async Task<ActionResult<MaintenanceReminderTestResult>> TestReminder(
        Guid scheduleId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new TestMaintenanceReminderCommand(scheduleId), cancellationToken);
        return Ok(result);
    }

    // ── Global fallback recipients (site admin mobiles/emails) ──
    [HttpGet("default-recipients")]
    [RequirePermission(AdminPermissions.Maintenance.View)]
    public async Task<ActionResult<IReadOnlyList<MaintenanceRecipientDto>>> GetDefaultRecipients(
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new GetMaintenanceDefaultRecipientsQuery(), cancellationToken));
    }

    /// <summary>
    /// Replaces the fallback list used by every schedule that has no recipients
    /// of its own. Any number of mobiles/emails can be registered.
    /// </summary>
    [HttpPut("default-recipients")]
    [RequirePermission(AdminPermissions.Maintenance.Update)]
    public async Task<ActionResult<IReadOnlyList<MaintenanceRecipientDto>>> ReplaceDefaultRecipients(
        [FromBody] ReplaceMaintenanceDefaultRecipientsCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command, cancellationToken));
    }

    // ── Service / repair cost logs ──
    [HttpGet("service-logs")]
    [RequirePermission(AdminPermissions.Maintenance.View)]
    public async Task<ActionResult<IReadOnlyList<MaintenanceServiceLogDto>>> GetServiceLogs(
        [FromQuery] Guid? equipmentId,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new GetMaintenanceServiceLogsQuery(equipmentId), cancellationToken));
    }

    [HttpPost("equipments/{equipmentId:guid}/service-logs")]
    [RequirePermission(AdminPermissions.Maintenance.Update)]
    public async Task<ActionResult<Guid>> CreateServiceLog(
        Guid equipmentId,
        [FromBody] CreateMaintenanceServiceLogCommand command,
        CancellationToken cancellationToken)
    {
        var id = await Mediator.Send(command with { EquipmentId = equipmentId }, cancellationToken);
        return Ok(id);
    }

    [HttpDelete("equipments/{equipmentId:guid}/service-logs/{id:guid}")]
    [RequirePermission(AdminPermissions.Maintenance.Delete)]
    public async Task<IActionResult> DeleteServiceLog(
        Guid equipmentId,
        Guid id,
        CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteMaintenanceServiceLogCommand(equipmentId, id), cancellationToken);
        return NoContent();
    }
}
