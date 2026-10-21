using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Api.Authorization;
using Sugarista.Application.Common.Models;
using Sugarista.Application.Hr.Employees.CreateEmployee;
using Sugarista.Application.Hr.Employees.GetEmployeeDetail;
using Sugarista.Application.Hr.Employees.GetEmployees;
using Sugarista.Application.Hr.Employees.SetEmployeeActive;
using Sugarista.Application.Hr.Employees.UpdateEmployee;
using Sugarista.Application.Hr.Employees.UpsertPayPlan;
using Sugarista.Application.Hr.Glossary.GetHrGlossary;
using Sugarista.Application.Hr.Glossary.UpdateHrGlossaryTerm;
using Sugarista.Application.Hr.Periods.AdjustPayrollEntry;
using Sugarista.Application.Hr.Periods.CreatePayrollPeriod;
using Sugarista.Application.Hr.Periods.FinalizePayrollPeriod;
using Sugarista.Application.Hr.Periods.GetPayrollPeriodDetail;
using Sugarista.Application.Hr.Periods.GetPayrollPeriods;
using Sugarista.Application.Hr.Periods.PayPayrollEntry;
using Sugarista.Application.Hr.Periods.PayPayrollPeriod;
using Sugarista.Application.Hr.Periods.ReopenPayrollPeriod;
using Sugarista.Application.Hr.OpenHours.GetCafeOpenHours;
using Sugarista.Application.Hr.OpenHours.UpdateCafeOpenHours;
using Sugarista.Application.Hr.Punches.GetPunches;
using Sugarista.Application.Hr.Punches.GetPunchStatus;
using Sugarista.Application.Hr.Punches.PunchIn;
using Sugarista.Application.Hr.Punches.PunchOut;
using Sugarista.Application.Hr.Shifts.DeleteShiftPlan;
using Sugarista.Application.Hr.Shifts.GetShiftPlans;
using Sugarista.Application.Hr.Shifts.UpsertShiftPlan;
using Sugarista.Application.Users.Roles.CreateAdminRole;
using Sugarista.Application.Users.Roles.DeleteAdminRole;
using Sugarista.Application.Users.Roles.GetAdminRoles;
using Sugarista.Application.Users.Roles.UpdateAdminRole;
using Sugarista.Application.Hr.WorkLogs.DeleteEmployeeWorkLog;
using Sugarista.Application.Hr.WorkLogs.GetEmployeeWorkLogs;
using Sugarista.Application.Hr.WorkLogs.SaveEmployeeWorkLog;
using Sugarista.Domain.Constants;

namespace Sugarista.Api.Controllers;

/// <summary>HR module: staff files, pay plans, work logs, payroll periods and glossary.</summary>
[Authorize]
[Route("api/admin/hr")]
public class AdminHrController : ApiControllerBase
{
    private const string View = AdminPermissions.Hr.View;
    private const string Mutate = AdminPermissions.Hr.Update;

    // ── Employees ────────────────────────────────────────────────

    [HttpGet("employees")]
    [RequirePermission(View)]
    public async Task<IActionResult> GetEmployees(
        [FromQuery] int page = 1,
        [FromQuery] string? search = null,
        [FromQuery] bool? activeOnly = null,
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(
            new GetEmployeesQuery(page, 25, search, activeOnly),
            cancellationToken));
    }

    [HttpGet("employees/{id:guid}")]
    [RequirePermission(View)]
    public async Task<IActionResult> GetEmployeeDetail(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new GetEmployeeDetailQuery(id), cancellationToken));
    }

    [HttpPost("employees")]
    [RequirePermission(AdminPermissions.Hr.Create)]
    public async Task<IActionResult> CreateEmployee(
        [FromBody] CreateEmployeeCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command, cancellationToken));
    }

    [HttpPut("employees/{id:guid}")]
    [RequirePermission(Mutate)]
    public async Task<IActionResult> UpdateEmployee(
        Guid id,
        [FromBody] UpdateEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new UpdateEmployeeCommand(
            id,
            request.PersianName,
            request.EnglishName,
            request.PhoneNumber,
            request.NationalId,
            request.Position,
            request.HireDate,
            request.Notes), cancellationToken));
    }

    [HttpPatch("employees/{id:guid}/active")]
    [RequirePermission(Mutate)]
    public async Task<IActionResult> SetActive(
        Guid id,
        [FromBody] SetEmployeeActiveRequest request,
        CancellationToken cancellationToken)
    {
        await Mediator.Send(
            new SetEmployeeActiveCommand(id, request.IsActive, request.TerminationDate),
            cancellationToken);
        return NoContent();
    }

    [HttpPost("employees/{id:guid}/pay-plan")]
    [RequirePermission(AdminPermissions.Hr.Create)]
    public async Task<IActionResult> UpsertPayPlan(
        Guid id,
        [FromBody] UpsertEmployeePayPlanRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new UpsertEmployeePayPlanCommand(
            id,
            request.Kind,
            request.SalaryAmount,
            request.HourlyRate,
            request.ShiftRate,
            request.CommissionPercent,
            request.OvertimeMultiplier,
            request.Notes), cancellationToken));
    }

    // ── Work logs ────────────────────────────────────────────────

    [HttpGet("work-logs")]
    [RequirePermission(View)]
    public async Task<IActionResult> GetWorkLogs(
        [FromQuery] Guid? employeeId = null,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        [FromQuery] int page = 1,
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(
            new GetEmployeeWorkLogsQuery(employeeId, from, to, page, 50),
            cancellationToken));
    }

    [HttpPost("work-logs")]
    [RequirePermission(Mutate)]
    public async Task<IActionResult> SaveWorkLog(
        [FromBody] SaveEmployeeWorkLogRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new SaveEmployeeWorkLogCommand(
            request.EmployeeId,
            request.WorkDate,
            request.WorkedHours,
            request.OvertimeHours,
            request.ShiftCount,
            request.Note), cancellationToken));
    }

    [HttpDelete("work-logs/{id:guid}")]
    [RequirePermission(AdminPermissions.Hr.Delete)]
    public async Task<IActionResult> DeleteWorkLog(
        Guid id,
        CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteEmployeeWorkLogCommand(id), cancellationToken);
        return NoContent();
    }

    // ── Payroll periods ──────────────────────────────────────────

    [HttpGet("periods")]
    [RequirePermission(View)]
    public async Task<IActionResult> GetPeriods(CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new GetPayrollPeriodsQuery(), cancellationToken));
    }

    [HttpGet("periods/{id:guid}")]
    [RequirePermission(View)]
    public async Task<IActionResult> GetPeriodDetail(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new GetPayrollPeriodDetailQuery(id), cancellationToken));
    }

    [HttpPost("periods")]
    [RequirePermission(Mutate)]
    public async Task<IActionResult> CreatePeriod(
        [FromBody] CreatePayrollPeriodRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(
            new CreatePayrollPeriodCommand(request.ShamsiYear, request.ShamsiMonth),
            cancellationToken));
    }

    [HttpPut("periods/{periodId:guid}/entries/{entryId:guid}")]
    [RequirePermission(Mutate)]
    public async Task<IActionResult> AdjustEntry(
        Guid periodId,
        Guid entryId,
        [FromBody] AdjustPayrollEntryRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new AdjustPayrollEntryCommand(
            periodId,
            entryId,
            request.CommissionSalesAmount,
            request.Bonus,
            request.Deductions,
            request.Advance,
            request.Note), cancellationToken));
    }

    [HttpPost("periods/{periodId:guid}/finalize")]
    [RequirePermission(Mutate)]
    public async Task<IActionResult> Finalize(
        Guid periodId,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new FinalizePayrollPeriodCommand(periodId), cancellationToken));
    }

    [HttpPost("periods/{periodId:guid}/reopen")]
    [RequirePermission(Mutate)]
    public async Task<IActionResult> Reopen(
        Guid periodId,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new ReopenPayrollPeriodCommand(periodId), cancellationToken));
    }

    [HttpPost("periods/{periodId:guid}/entries/{entryId:guid}/pay")]
    [RequirePermission(Mutate)]
    public async Task<IActionResult> PayEntry(
        Guid periodId,
        Guid entryId,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new PayPayrollEntryCommand(periodId, entryId), cancellationToken));
    }

    [HttpPost("periods/{periodId:guid}/pay-all")]
    [RequirePermission(Mutate)]
    public async Task<IActionResult> PayAll(
        Guid periodId,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new PayPayrollPeriodCommand(periodId), cancellationToken));
    }

    // ── Café opening hours (ساعات بازگشایی) ──────────────────────

    [HttpGet("open-hours")]
    [RequirePermission(View)]
    public async Task<IActionResult> GetOpenHours(CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new GetCafeOpenHoursQuery(), cancellationToken));
    }

    [HttpPut("open-hours")]
    [RequirePermission(Mutate)]
    public async Task<IActionResult> UpdateOpenHours(
        [FromBody] UpdateCafeOpenHoursRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(
            new UpdateCafeOpenHoursCommand(request.Days), cancellationToken));
    }

    // ── Approved shift plans (شیفت مصوب) ─────────────────────────

    [HttpGet("shifts")]
    [RequirePermission(View)]
    public async Task<IActionResult> GetShiftPlans(
        [FromQuery] Guid? employeeId = null,
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(new GetShiftPlansQuery(employeeId), cancellationToken));
    }

    [HttpPost("shifts")]
    [RequirePermission(Mutate)]
    public async Task<IActionResult> UpsertShiftPlan(
        [FromBody] UpsertShiftPlanRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new UpsertShiftPlanCommand(
            request.EmployeeId,
            request.Weekday,
            request.StartTime,
            request.EndTime), cancellationToken));
    }

    [HttpDelete("shifts/{id:guid}")]
    [RequirePermission(AdminPermissions.Hr.Delete)]
    public async Task<IActionResult> DeleteShiftPlan(
        Guid id,
        CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteShiftPlanCommand(id), cancellationToken);
        return NoContent();
    }

    // ── Punch in / out (حضور واقعی) ──────────────────────────────

    [HttpGet("punches/status")]
    [RequirePermission(View)]
    public async Task<IActionResult> GetPunchStatus(
        [FromQuery] Guid? employeeId = null,
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(new GetPunchStatusQuery(), cancellationToken));
    }

    [HttpGet("punches")]
    [RequirePermission(View)]
    public async Task<IActionResult> GetPunches(
        [FromQuery] Guid? employeeId = null,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        [FromQuery] int page = 1,
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(
            new GetPunchesQuery(employeeId, from, to, page, 50),
            cancellationToken));
    }

    [HttpPost("punches/in")]
    [RequirePermission(Mutate)]
    public async Task<IActionResult> PunchIn(
        [FromBody] PunchInRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new PunchInCommand(request.EmployeeId, request.Note), cancellationToken));
    }

    [HttpPost("punches/out")]
    [RequirePermission(Mutate)]
    public async Task<IActionResult> PunchOut(
        [FromBody] PunchOutRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new PunchOutCommand(request.EmployeeId), cancellationToken));
    }

    // ── Glossary ─────────────────────────────────────────────────

    [HttpGet("glossary")]
    [RequirePermission(View)]
    public async Task<IActionResult> GetGlossary(CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new GetHrGlossaryQuery(), cancellationToken));
    }

    [HttpPut("glossary/{key}")]
    [RequirePermission(Mutate)]
    public async Task<IActionResult> UpdateGlossaryTerm(
        string key,
        [FromBody] UpdateHrGlossaryTermRequest request,
        CancellationToken cancellationToken)
    {
        await Mediator.Send(new UpdateHrGlossaryTermCommand(key, request.Definition), cancellationToken);
        return NoContent();
    }
}

public record UpdateEmployeeRequest(
    string PersianName,
    string? EnglishName,
    string? PhoneNumber,
    string? NationalId,
    string Position,
    DateOnly HireDate,
    string? Notes);

public record SetEmployeeActiveRequest(
    bool IsActive,
    DateOnly? TerminationDate);

public record UpsertEmployeePayPlanRequest(
    string Kind,
    decimal SalaryAmount,
    decimal HourlyRate,
    decimal ShiftRate,
    decimal CommissionPercent,
    decimal OvertimeMultiplier,
    string? Notes);

public record SaveEmployeeWorkLogRequest(
    Guid EmployeeId,
    DateOnly WorkDate,
    decimal WorkedHours,
    decimal OvertimeHours,
    int ShiftCount,
    string? Note);

public record CreatePayrollPeriodRequest(int ShamsiYear, int ShamsiMonth);

public record AdjustPayrollEntryRequest(
    decimal CommissionSalesAmount,
    decimal Bonus,
    decimal Deductions,
    decimal Advance,
    string? Note);

public record UpdateHrGlossaryTermRequest(string Definition);

public record UpdateCafeOpenHoursRequest(IReadOnlyList<CafeOpenHourInput> Days);

public record UpsertShiftPlanRequest(
    Guid EmployeeId,
    int Weekday,
    string StartTime,
    string EndTime);

public record PunchInRequest(
    Guid EmployeeId,
    string? Note);

public record PunchOutRequest(Guid EmployeeId);
