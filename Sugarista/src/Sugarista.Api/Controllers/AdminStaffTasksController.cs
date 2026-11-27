using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sugarista.Api.Authorization;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Constants;
using Sugarista.Domain.Entities;

namespace Sugarista.Api.Controllers;

[Authorize]
[Route("api/admin/staff-tasks")]
public class AdminStaffTasksController : ApiControllerBase
{
    private readonly IApplicationDbContext _dbContext;

    public AdminStaffTasksController(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Returns all currently DUE and uncompleted tasks assigned to the logged-in user.
    /// Open to every authenticated admin/staff user so persistent anti-forgetting toasts
    /// always raise on time regardless of which page they are working on.
    /// </summary>
    [HttpGet("my-due")]
    public async Task<ActionResult<MyDueStaffTasksResponseDto>> GetMyDueTasks(CancellationToken cancellationToken)
    {
        var currentUserId = TryGetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            return Unauthorized();
        }

        var nowUtc = DateTimeOffset.UtcNow;
        var tehranNow = nowUtc.ToOffset(StaffTask.TehranOffset);

        var myTasks = await _dbContext.StaffTasks
            .AsNoTracking()
            .Where(t => t.IsActive && t.AssignedToAdminUserId == currentUserId.Value)
            .ToListAsync(cancellationToken);

        var dueCandidates = new List<(StaffTask Task, DateOnly OccurrenceDate, DateTimeOffset ScheduledAtUtc)>();
        foreach (var task in myTasks)
        {
            var dueDate = task.GetDueOccurrenceDateAt(nowUtc);
            if (dueDate.HasValue)
            {
                dueCandidates.Add((task, dueDate.Value, task.GetScheduledAtUtc(dueDate.Value)));
            }
        }

        if (dueCandidates.Count == 0)
        {
            return Ok(new MyDueStaffTasksResponseDto(
                ServerUtc: nowUtc,
                TehranTime: tehranNow.ToString("HH:mm", CultureInfo.InvariantCulture),
                Items: []));
        }

        var taskIds = dueCandidates.Select(c => c.Task.Id).Distinct().ToList();
        var dates = dueCandidates.Select(c => c.OccurrenceDate).Distinct().ToList();

        var completedKeys = await _dbContext.StaffTaskCompletions
            .AsNoTracking()
            .Where(c => taskIds.Contains(c.StaffTaskId) && dates.Contains(c.OccurrenceDate))
            .Select(c => new { c.StaffTaskId, c.OccurrenceDate })
            .ToListAsync(cancellationToken);

        var completedSet = completedKeys
            .Select(k => (k.StaffTaskId, k.OccurrenceDate))
            .ToHashSet();

        var dueItems = dueCandidates
            .Where(c => !completedSet.Contains((c.Task.Id, c.OccurrenceDate)))
            .Select(c =>
            {
                var overdueMinutes = Math.Max(0, (int)Math.Floor((nowUtc - c.ScheduledAtUtc).TotalMinutes));
                return new DueStaffTaskToastDto(
                    Id: c.Task.Id,
                    Title: c.Task.Title,
                    Description: c.Task.Description,
                    Category: c.Task.Category,
                    Priority: c.Task.Priority.ToString(),
                    RecurrenceType: c.Task.RecurrenceType.ToString(),
                    RaiseTime: c.Task.RaiseTimeOfDay.ToString("HH:mm", CultureInfo.InvariantCulture),
                    OccurrenceDate: c.OccurrenceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    ScheduledAtUtc: c.ScheduledAtUtc,
                    OverdueMinutes: overdueMinutes,
                    RequireNoteOnComplete: c.Task.RequireNoteOnComplete);
            })
            .OrderByDescending(i => i.Priority == nameof(StaffTaskPriority.Critical) ? 2 : i.Priority == nameof(StaffTaskPriority.High) ? 1 : 0)
            .ThenByDescending(i => i.OverdueMinutes)
            .ThenBy(i => i.RaiseTime, StringComparer.Ordinal)
            .ToList();

        return Ok(new MyDueStaffTasksResponseDto(
            ServerUtc: nowUtc,
            TehranTime: tehranNow.ToString("HH:mm", CultureInfo.InvariantCulture),
            Items: dueItems));
    }

    /// <summary>
    /// Lists staff tasks with live today-status.
    /// Users with StaffTasks.ViewAll / Create / Update / Delete or SuperAdmin see all tasks;
    /// regular staff see only tasks assigned to themselves.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<StaffTaskBoardResponseDto>> GetBoard(
        [FromQuery] Guid? assignedToAdminUserId = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = TryGetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            return Unauthorized();
        }

        var canViewAll = CanViewAllTasks();
        var nowUtc = DateTimeOffset.UtcNow;
        var tehranNow = nowUtc.ToOffset(StaffTask.TehranOffset);
        var tehranToday = StaffTask.ToTehranDate(nowUtc);

        var query = _dbContext.StaffTasks
            .AsNoTracking()
            .Include(t => t.AssignedToAdminUser)
            .AsQueryable();

        if (!canViewAll)
        {
            query = query.Where(t => t.AssignedToAdminUserId == currentUserId.Value);
        }
        else if (assignedToAdminUserId.HasValue)
        {
            query = query.Where(t => t.AssignedToAdminUserId == assignedToAdminUserId.Value);
        }

        var tasks = await query
            .OrderByDescending(t => t.IsActive)
            .ThenBy(t => t.RaiseTimeOfDay)
            .ThenBy(t => t.Title)
            .ToListAsync(cancellationToken);

        var taskIds = tasks.Select(t => t.Id).ToList();
        var relevantCompletions = taskIds.Count == 0
            ? []
            : await _dbContext.StaffTaskCompletions
                .AsNoTracking()
                .Where(c => taskIds.Contains(c.StaffTaskId) &&
                            (c.OccurrenceDate == tehranToday ||
                             c.StaffTask.RecurrenceType == StaffTaskRecurrenceType.OneTime))
                .OrderByDescending(c => c.CompletedAtUtc)
                .ToListAsync(cancellationToken);

        var completionsByTask = relevantCompletions
            .GroupBy(c => c.StaffTaskId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var rows = new List<StaffTaskBoardItemDto>(tasks.Count);
        int totalToday = 0, dueNowCount = 0, doneTodayCount = 0, upcomingTodayCount = 0;

        foreach (var task in tasks)
        {
            completionsByTask.TryGetValue(task.Id, out var taskCompletions);
            var targetDate = task.RecurrenceType == StaffTaskRecurrenceType.OneTime && task.OneTimeDate.HasValue
                ? task.OneTimeDate.Value
                : tehranToday;

            var completion = taskCompletions?.FirstOrDefault(c => c.OccurrenceDate == targetDate);
            var scheduledToday = task.RecurrenceType == StaffTaskRecurrenceType.OneTime
                ? task.OneTimeDate.HasValue && task.OneTimeDate.Value <= tehranToday
                : task.IsScheduledForTehranDate(tehranToday);

            var scheduledAtUtc = task.GetScheduledAtUtc(targetDate);
            string todayState;
            int overdueMinutes = 0;

            if (!task.IsActive)
            {
                todayState = "Inactive";
            }
            else if (completion is not null)
            {
                todayState = "Done";
                if (scheduledToday)
                {
                    totalToday++;
                    doneTodayCount++;
                }
            }
            else if (task.GetDueOccurrenceDateAt(nowUtc) is { } dueDate)
            {
                todayState = "Due";
                var dueScheduled = task.GetScheduledAtUtc(dueDate);
                overdueMinutes = Math.Max(0, (int)Math.Floor((nowUtc - dueScheduled).TotalMinutes));
                totalToday++;
                dueNowCount++;
            }
            else if (scheduledToday)
            {
                todayState = "Upcoming";
                totalToday++;
                upcomingTodayCount++;
            }
            else
            {
                todayState = "NotScheduledToday";
            }

            rows.Add(new StaffTaskBoardItemDto(
                Id: task.Id,
                Title: task.Title,
                Description: task.Description,
                AssignedToAdminUserId: task.AssignedToAdminUserId,
                AssignedToDisplayName: task.AssignedToAdminUser?.DisplayName ?? "—",
                AssignedToUsername: task.AssignedToAdminUser?.Username ?? string.Empty,
                Category: task.Category,
                Priority: task.Priority.ToString(),
                RecurrenceType: task.RecurrenceType.ToString(),
                DaysOfWeekMask: task.DaysOfWeekMask,
                OneTimeDate: task.OneTimeDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                RaiseTime: task.RaiseTimeOfDay.ToString("HH:mm", CultureInfo.InvariantCulture),
                RequireNoteOnComplete: task.RequireNoteOnComplete,
                IsActive: task.IsActive,
                TodayState: todayState,
                OccurrenceDate: targetDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ScheduledAtUtc: scheduledAtUtc,
                OverdueMinutes: overdueMinutes,
                CompletedAtUtc: completion?.CompletedAtUtc,
                CompletedByDisplayName: completion?.CompletedByDisplayName,
                DelayMinutes: completion?.DelayMinutes,
                CompletionNote: completion?.Note,
                IsAssignedToCurrentUser: task.AssignedToAdminUserId == currentUserId.Value));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            rows = rows
                .Where(r => string.Equals(r.TodayState, status.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        List<StaffTaskAssigneeDto> assignees = [];
        if (canViewAll)
        {
            assignees = await _dbContext.AdminUsers
                .AsNoTracking()
                .Where(u => u.IsActive)
                .OrderBy(u => u.DisplayName)
                .Select(u => new StaffTaskAssigneeDto(u.Id, u.DisplayName, u.Username, u.Role, u.IsSuperAdmin))
                .ToListAsync(cancellationToken);
        }

        return Ok(new StaffTaskBoardResponseDto(
            ServerUtc: nowUtc,
            TehranDate: tehranToday.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            TehranTime: tehranNow.ToString("HH:mm", CultureInfo.InvariantCulture),
            CanViewAll: canViewAll,
            Summary: new StaffTaskSummaryDto(
                TotalTasks: tasks.Count,
                ActiveTasks: tasks.Count(t => t.IsActive),
                TotalToday: totalToday,
                DueNow: dueNowCount,
                DoneToday: doneTodayCount,
                UpcomingToday: upcomingTodayCount),
            Assignees: assignees,
            Items: rows));
    }

    [HttpPost]
    [RequirePermission(AdminPermissions.StaffTasks.Create)]
    public async Task<ActionResult<Guid>> Create(
        [FromBody] UpsertStaffTaskRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseTime(request.RaiseTime, out var raiseTime))
        {
            return BadRequest(new { detail = "ساعت اجرا معتبر نیست (قالب صحیح: HH:mm مثلاً 21:00)." });
        }

        var assignee = await _dbContext.AdminUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.AssignedToAdminUserId && u.IsActive, cancellationToken);
        if (assignee is null)
        {
            return BadRequest(new { detail = "کاربر پرسنل انتخاب‌شده معتبر یا فعال نیست." });
        }

        var priority = Enum.TryParse<StaffTaskPriority>(request.Priority, true, out var parsedPriority)
            ? parsedPriority
            : StaffTaskPriority.Normal;

        var recurrence = Enum.TryParse<StaffTaskRecurrenceType>(request.RecurrenceType, true, out var parsedRec)
            ? parsedRec
            : StaffTaskRecurrenceType.Daily;

        DateOnly? oneTimeDate = null;
        if (recurrence == StaffTaskRecurrenceType.OneTime)
        {
            if (string.IsNullOrWhiteSpace(request.OneTimeDate) ||
                !DateOnly.TryParseExact(request.OneTimeDate.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
            {
                return BadRequest(new { detail = "برای تسک یک‌باره تاریخ معتبر انتخاب کنید." });
            }
            oneTimeDate = parsedDate;
        }

        var task = new StaffTask(
            request.Title,
            request.Description,
            assignee.Id,
            request.Category,
            priority,
            recurrence,
            raiseTime,
            request.DaysOfWeekMask ?? StaffTask.AllDaysMask,
            oneTimeDate,
            request.RequireNoteOnComplete,
            request.IsActive);

        _dbContext.StaffTasks.Add(task);

        var (actorId, actorName) = await GetCurrentActorAsync(cancellationToken);
        _dbContext.StaffTaskActivityLogs.Add(new StaffTaskActivityLog(
            task.Id,
            task.Title,
            assignee.DisplayName,
            "Created",
            actorId,
            actorName,
            $"ساعت {raiseTime:HH:mm} — نوع: {DescribeRecurrence(recurrence)} — مسئول: {assignee.DisplayName}",
            DateTimeOffset.UtcNow));

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(task.Id);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(AdminPermissions.StaffTasks.Update)]
    public async Task<ActionResult<Guid>> Update(
        Guid id,
        [FromBody] UpsertStaffTaskRequest request,
        CancellationToken cancellationToken)
    {
        var task = await _dbContext.StaffTasks.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (task is null)
        {
            return NotFound(new { detail = "تسک مورد نظر یافت نشد." });
        }

        if (!TryParseTime(request.RaiseTime, out var raiseTime))
        {
            return BadRequest(new { detail = "ساعت اجرا معتبر نیست (قالب صحیح: HH:mm مثلاً 21:00)." });
        }

        var assignee = await _dbContext.AdminUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.AssignedToAdminUserId && u.IsActive, cancellationToken);
        if (assignee is null)
        {
            return BadRequest(new { detail = "کاربر پرسنل انتخاب‌شده معتبر یا فعال نیست." });
        }

        var priority = Enum.TryParse<StaffTaskPriority>(request.Priority, true, out var parsedPriority)
            ? parsedPriority
            : StaffTaskPriority.Normal;

        var recurrence = Enum.TryParse<StaffTaskRecurrenceType>(request.RecurrenceType, true, out var parsedRec)
            ? parsedRec
            : StaffTaskRecurrenceType.Daily;

        DateOnly? oneTimeDate = null;
        if (recurrence == StaffTaskRecurrenceType.OneTime)
        {
            if (string.IsNullOrWhiteSpace(request.OneTimeDate) ||
                !DateOnly.TryParseExact(request.OneTimeDate.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
            {
                return BadRequest(new { detail = "برای تسک یک‌باره تاریخ معتبر انتخاب کنید." });
            }
            oneTimeDate = parsedDate;
        }

        task.Update(
            request.Title,
            request.Description,
            assignee.Id,
            request.Category,
            priority,
            recurrence,
            raiseTime,
            request.DaysOfWeekMask ?? StaffTask.AllDaysMask,
            oneTimeDate,
            request.RequireNoteOnComplete,
            request.IsActive);

        var (actorId, actorName) = await GetCurrentActorAsync(cancellationToken);
        _dbContext.StaffTaskActivityLogs.Add(new StaffTaskActivityLog(
            task.Id,
            task.Title,
            assignee.DisplayName,
            "Updated",
            actorId,
            actorName,
            $"ویرایش شد — ساعت {raiseTime:HH:mm} — مسئول: {assignee.DisplayName}",
            DateTimeOffset.UtcNow));

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(task.Id);
    }

    [HttpPatch("{id:guid}/toggle")]
    [RequirePermission(AdminPermissions.StaffTasks.Update)]
    public async Task<IActionResult> ToggleActive(Guid id, CancellationToken cancellationToken)
    {
        var task = await _dbContext.StaffTasks
            .Include(t => t.AssignedToAdminUser)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (task is null)
        {
            return NotFound(new { detail = "تسک مورد نظر یافت نشد." });
        }

        task.SetActive(!task.IsActive);

        var (actorId, actorName) = await GetCurrentActorAsync(cancellationToken);
        _dbContext.StaffTaskActivityLogs.Add(new StaffTaskActivityLog(
            task.Id,
            task.Title,
            task.AssignedToAdminUser?.DisplayName ?? "—",
            task.IsActive ? "Activated" : "Deactivated",
            actorId,
            actorName,
            task.IsActive ? "تسک فعال شد." : "تسک موقتاً غیرفعال شد.",
            DateTimeOffset.UtcNow));

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new { id = task.Id, isActive = task.IsActive });
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(AdminPermissions.StaffTasks.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var task = await _dbContext.StaffTasks
            .Include(t => t.AssignedToAdminUser)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (task is null)
        {
            return NotFound(new { detail = "تسک مورد نظر یافت نشد." });
        }

        var (actorId, actorName) = await GetCurrentActorAsync(cancellationToken);
        _dbContext.StaffTaskActivityLogs.Add(new StaffTaskActivityLog(
            null,
            task.Title,
            task.AssignedToAdminUser?.DisplayName ?? "—",
            "Deleted",
            actorId,
            actorName,
            $"تسک حذف شد (ساعت مقرر: {task.RaiseTimeOfDay:HH:mm}).",
            DateTimeOffset.UtcNow));

        _dbContext.StaffTasks.Remove(task);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Marks a task occurrence as DONE. Allowed for the assigned staff member (always)
    /// or a manager with StaffTasks.Update / SuperAdmin.
    /// </summary>
    [HttpPost("{id:guid}/complete")]
    public async Task<ActionResult<StaffTaskCompletionDto>> Complete(
        Guid id,
        [FromBody] CompleteStaffTaskRequest? request,
        CancellationToken cancellationToken)
    {
        var currentUserId = TryGetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            return Unauthorized();
        }

        var task = await _dbContext.StaffTasks
            .Include(t => t.AssignedToAdminUser)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (task is null)
        {
            return NotFound(new { detail = "تسک مورد نظر یافت نشد." });
        }

        var isAssignee = task.AssignedToAdminUserId == currentUserId.Value;
        var canManage = IsSuperAdmin() || HasPermissionClaim(AdminPermissions.StaffTasks.Update);
        if (!isAssignee && !canManage)
        {
            return Forbid();
        }

        var note = request?.Note?.Trim();
        if (task.RequireNoteOnComplete && string.IsNullOrWhiteSpace(note))
        {
            return BadRequest(new { detail = "برای این تسک، نوشتن توضیح کوتاه هنگام انجام الزامی است." });
        }

        var nowUtc = DateTimeOffset.UtcNow;
        var occurrenceDate = task.RecurrenceType == StaffTaskRecurrenceType.OneTime && task.OneTimeDate.HasValue
            ? task.OneTimeDate.Value
            : StaffTask.ToTehranDate(nowUtc);

        if (!string.IsNullOrWhiteSpace(request?.OccurrenceDate) &&
            DateOnly.TryParseExact(request.OccurrenceDate.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var requestedDate))
        {
            occurrenceDate = requestedDate;
        }

        var existing = await _dbContext.StaffTaskCompletions
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.StaffTaskId == task.Id && c.OccurrenceDate == occurrenceDate, cancellationToken);
        if (existing is not null)
        {
            return Ok(ToCompletionDto(existing, task.Title, task.Category, task.AssignedToAdminUser?.DisplayName ?? "—"));
        }

        var (actorId, actorName) = await GetCurrentActorAsync(cancellationToken);
        var scheduledAtUtc = task.GetScheduledAtUtc(occurrenceDate);

        var completion = new StaffTaskCompletion(
            task.Id,
            occurrenceDate,
            scheduledAtUtc,
            nowUtc,
            actorId ?? currentUserId.Value,
            actorName,
            note);

        _dbContext.StaffTaskCompletions.Add(completion);
        _dbContext.StaffTaskActivityLogs.Add(new StaffTaskActivityLog(
            task.Id,
            task.Title,
            task.AssignedToAdminUser?.DisplayName ?? "—",
            "Completed",
            actorId,
            actorName,
            completion.DelayMinutes == 0
                ? $"به‌موقع انجام شد{(string.IsNullOrWhiteSpace(note) ? "" : $" — توضیح: {note}")}"
                : $"با {completion.DelayMinutes} دقیقه تاخیر انجام شد{(string.IsNullOrWhiteSpace(note) ? "" : $" — توضیح: {note}")}",
            nowUtc));

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToCompletionDto(completion, task.Title, task.Category, task.AssignedToAdminUser?.DisplayName ?? "—"));
    }

    [HttpPost("{id:guid}/reopen")]
    [RequirePermission(AdminPermissions.StaffTasks.Update)]
    public async Task<IActionResult> Reopen(
        Guid id,
        [FromQuery] string? occurrenceDate = null,
        CancellationToken cancellationToken = default)
    {
        var task = await _dbContext.StaffTasks
            .Include(t => t.AssignedToAdminUser)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (task is null)
        {
            return NotFound(new { detail = "تسک مورد نظر یافت نشد." });
        }

        var nowUtc = DateTimeOffset.UtcNow;
        var targetDate = task.RecurrenceType == StaffTaskRecurrenceType.OneTime && task.OneTimeDate.HasValue
            ? task.OneTimeDate.Value
            : StaffTask.ToTehranDate(nowUtc);

        if (!string.IsNullOrWhiteSpace(occurrenceDate) &&
            DateOnly.TryParseExact(occurrenceDate.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            targetDate = parsed;
        }

        var completion = await _dbContext.StaffTaskCompletions
            .FirstOrDefaultAsync(c => c.StaffTaskId == task.Id && c.OccurrenceDate == targetDate, cancellationToken);
        if (completion is null)
        {
            return Ok(new { reopened = false });
        }

        _dbContext.StaffTaskCompletions.Remove(completion);

        var (actorId, actorName) = await GetCurrentActorAsync(cancellationToken);
        _dbContext.StaffTaskActivityLogs.Add(new StaffTaskActivityLog(
            task.Id,
            task.Title,
            task.AssignedToAdminUser?.DisplayName ?? "—",
            "Reopened",
            actorId,
            actorName,
            $"انجام تسک برای تاریخ {targetDate:yyyy-MM-dd} بازگشایی شد.",
            nowUtc));

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new { reopened = true });
    }

    [HttpGet("logs")]
    [RequirePermission(AdminPermissions.StaffTasks.ViewLogs)]
    public async Task<ActionResult<StaffTaskLogsResponseDto>> GetLogs(
        [FromQuery] Guid? adminUserId = null,
        [FromQuery] Guid? staffTaskId = null,
        [FromQuery] int days = 30,
        CancellationToken cancellationToken = default)
    {
        var safeDays = Math.Clamp(days, 1, 365);
        var sinceUtc = DateTimeOffset.UtcNow.AddDays(-safeDays);

        var completionsQuery = _dbContext.StaffTaskCompletions
            .AsNoTracking()
            .Include(c => c.StaffTask)
                .ThenInclude(t => t.AssignedToAdminUser)
            .Where(c => c.CompletedAtUtc >= sinceUtc);

        if (adminUserId.HasValue)
        {
            completionsQuery = completionsQuery.Where(c =>
                c.CompletedByAdminUserId == adminUserId.Value ||
                c.StaffTask.AssignedToAdminUserId == adminUserId.Value);
        }

        if (staffTaskId.HasValue)
        {
            completionsQuery = completionsQuery.Where(c => c.StaffTaskId == staffTaskId.Value);
        }

        var completions = await completionsQuery
            .OrderByDescending(c => c.CompletedAtUtc)
            .Take(200)
            .Select(c => new StaffTaskCompletionDto(
                c.Id,
                c.StaffTaskId,
                c.StaffTask.Title,
                c.StaffTask.Category,
                c.StaffTask.AssignedToAdminUser.DisplayName,
                c.OccurrenceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                c.ScheduledAtUtc,
                c.CompletedAtUtc,
                c.CompletedByDisplayName,
                c.DelayMinutes,
                c.Note))
            .ToListAsync(cancellationToken);

        var activitiesQuery = _dbContext.StaffTaskActivityLogs
            .AsNoTracking()
            .Where(a => a.OccurredAtUtc >= sinceUtc);

        if (adminUserId.HasValue)
        {
            activitiesQuery = activitiesQuery.Where(a => a.ActorAdminUserId == adminUserId.Value);
        }

        if (staffTaskId.HasValue)
        {
            activitiesQuery = activitiesQuery.Where(a => a.StaffTaskId == staffTaskId.Value);
        }

        var activities = await activitiesQuery
            .OrderByDescending(a => a.OccurredAtUtc)
            .Take(200)
            .Select(a => new StaffTaskActivityLogDto(
                a.Id,
                a.StaffTaskId,
                a.TaskTitleSnapshot,
                a.AssignedToDisplayName,
                a.Action,
                a.ActorDisplayName,
                a.Details,
                a.OccurredAtUtc))
            .ToListAsync(cancellationToken);

        return Ok(new StaffTaskLogsResponseDto(completions, activities));
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private Guid? TryGetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    private bool IsSuperAdmin() =>
        string.Equals(
            User.FindFirstValue(AdminPermissions.SuperAdminClaimType),
            bool.TrueString,
            StringComparison.OrdinalIgnoreCase);

    private bool HasPermissionClaim(string permission) =>
        User.FindAll(AdminPermissions.ClaimType)
            .Any(c => string.Equals(c.Value, permission, StringComparison.Ordinal));

    private bool CanViewAllTasks() =>
        IsSuperAdmin()
        || HasPermissionClaim(AdminPermissions.StaffTasks.ViewAll)
        || HasPermissionClaim(AdminPermissions.StaffTasks.Create)
        || HasPermissionClaim(AdminPermissions.StaffTasks.Update)
        || HasPermissionClaim(AdminPermissions.StaffTasks.Delete);

    private async Task<(Guid? Id, string DisplayName)> GetCurrentActorAsync(CancellationToken cancellationToken)
    {
        var id = TryGetCurrentUserId();
        if (!id.HasValue)
        {
            return (null, "سیستم");
        }

        var displayName = await _dbContext.AdminUsers
            .AsNoTracking()
            .Where(u => u.Id == id.Value)
            .Select(u => u.DisplayName)
            .FirstOrDefaultAsync(cancellationToken);

        return (id, string.IsNullOrWhiteSpace(displayName) ? (User.Identity?.Name ?? "کاربر") : displayName);
    }

    private static bool TryParseTime(string? value, out TimeOnly time)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            time = default;
            return false;
        }

        var trimmed = value.Trim();
        return TimeOnly.TryParseExact(trimmed, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time)
               || TimeOnly.TryParseExact(trimmed, "H:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time)
               || TimeOnly.TryParseExact(trimmed, "HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
    }

    private static string DescribeRecurrence(StaffTaskRecurrenceType recurrence) =>
        recurrence switch
        {
            StaffTaskRecurrenceType.Daily => "روزانه",
            StaffTaskRecurrenceType.Weekly => "روزهای منتخب هفته",
            StaffTaskRecurrenceType.OneTime => "یک‌باره",
            _ => "روزانه"
        };

    private static StaffTaskCompletionDto ToCompletionDto(
        StaffTaskCompletion c,
        string taskTitle,
        string category,
        string assignedToName) =>
        new(
            c.Id,
            c.StaffTaskId,
            taskTitle,
            category,
            assignedToName,
            c.OccurrenceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            c.ScheduledAtUtc,
            c.CompletedAtUtc,
            c.CompletedByDisplayName,
            c.DelayMinutes,
            c.Note);
}

// ── DTOs ────────────────────────────────────────────────────────────────────

public record MyDueStaffTasksResponseDto(
    DateTimeOffset ServerUtc,
    string TehranTime,
    IReadOnlyList<DueStaffTaskToastDto> Items);

public record DueStaffTaskToastDto(
    Guid Id,
    string Title,
    string? Description,
    string Category,
    string Priority,
    string RecurrenceType,
    string RaiseTime,
    string OccurrenceDate,
    DateTimeOffset ScheduledAtUtc,
    int OverdueMinutes,
    bool RequireNoteOnComplete);

public record StaffTaskAssigneeDto(
    Guid Id,
    string DisplayName,
    string Username,
    string Role,
    bool IsSuperAdmin);

public record StaffTaskSummaryDto(
    int TotalTasks,
    int ActiveTasks,
    int TotalToday,
    int DueNow,
    int DoneToday,
    int UpcomingToday);

public record StaffTaskBoardItemDto(
    Guid Id,
    string Title,
    string? Description,
    Guid AssignedToAdminUserId,
    string AssignedToDisplayName,
    string AssignedToUsername,
    string Category,
    string Priority,
    string RecurrenceType,
    int DaysOfWeekMask,
    string? OneTimeDate,
    string RaiseTime,
    bool RequireNoteOnComplete,
    bool IsActive,
    string TodayState,
    string OccurrenceDate,
    DateTimeOffset ScheduledAtUtc,
    int OverdueMinutes,
    DateTimeOffset? CompletedAtUtc,
    string? CompletedByDisplayName,
    int? DelayMinutes,
    string? CompletionNote,
    bool IsAssignedToCurrentUser);

public record StaffTaskBoardResponseDto(
    DateTimeOffset ServerUtc,
    string TehranDate,
    string TehranTime,
    bool CanViewAll,
    StaffTaskSummaryDto Summary,
    IReadOnlyList<StaffTaskAssigneeDto> Assignees,
    IReadOnlyList<StaffTaskBoardItemDto> Items);

public record UpsertStaffTaskRequest(
    string Title,
    string? Description,
    Guid AssignedToAdminUserId,
    string? Category,
    string Priority,
    string RecurrenceType,
    string RaiseTime,
    int? DaysOfWeekMask,
    string? OneTimeDate,
    bool RequireNoteOnComplete,
    bool IsActive = true);

public record CompleteStaffTaskRequest(
    string? Note = null,
    string? OccurrenceDate = null);

public record StaffTaskCompletionDto(
    Guid Id,
    Guid StaffTaskId,
    string TaskTitle,
    string Category,
    string AssignedToDisplayName,
    string OccurrenceDate,
    DateTimeOffset ScheduledAtUtc,
    DateTimeOffset CompletedAtUtc,
    string CompletedByDisplayName,
    int DelayMinutes,
    string? Note);

public record StaffTaskActivityLogDto(
    Guid Id,
    Guid? StaffTaskId,
    string TaskTitleSnapshot,
    string AssignedToDisplayName,
    string Action,
    string ActorDisplayName,
    string? Details,
    DateTimeOffset OccurredAtUtc);

public record StaffTaskLogsResponseDto(
    IReadOnlyList<StaffTaskCompletionDto> Completions,
    IReadOnlyList<StaffTaskActivityLogDto> Activities);
