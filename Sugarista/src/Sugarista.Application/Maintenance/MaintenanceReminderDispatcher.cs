using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Maintenance;

/// <summary>
/// Sends maintenance service reminders (SMS + email) to the dynamic recipient
/// list of each schedule. Shared between the background ticker and the «ارسال
/// آزمایشی» admin command so the wording and gating stay in one place.
/// </summary>
public class MaintenanceReminderDispatcher
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IMaintenanceSmsSender _smsSender;
    private readonly IMaintenanceEmailSender _emailSender;
    private readonly ILogger<MaintenanceReminderDispatcher> _logger;

    public MaintenanceReminderDispatcher(
        IApplicationDbContext dbContext,
        IMaintenanceSmsSender smsSender,
        IMaintenanceEmailSender emailSender,
        ILogger<MaintenanceReminderDispatcher> logger)
    {
        _dbContext = dbContext;
        _smsSender = smsSender;
        _emailSender = emailSender;
        _logger = logger;
    }

    /// <summary>Today's date (Tehran) so schedules run on the café's calendar.</summary>
    public static DateOnly TehranToday()
    {
        var tehran = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tehran).DateTime);
    }

    /// <summary>
    /// Sends one reminder per due schedule, then rolls each schedule forward a
    /// full cycle so a «due» schedule only fires once per period. Returns the
    /// number of schedules reminded.
    /// </summary>
    public async Task<int> DispatchDueRemindersAsync(CancellationToken cancellationToken = default)
    {
        var today = TehranToday();
        var defaultRecipients = await LoadDefaultRecipientsAsync(cancellationToken);

        var dueSchedules = await _dbContext.MaintenanceSchedules
            .Include(schedule => schedule.Equipment)
            .Include(schedule => schedule.Recipients)
            .Where(schedule => schedule.IsActive
                && schedule.Equipment.IsActive
                && schedule.NextDueDate <= today)
            .OrderBy(schedule => schedule.NextDueDate)
            .ToListAsync(cancellationToken);

        var sent = 0;

        foreach (var schedule in dueSchedules)
        {
            try
            {
                var recipients = ResolveRecipients(schedule, defaultRecipients);
                if (recipients.Count == 0)
                {
                    // Nothing to send to: keep the schedule due so it fires as soon
                    // as the site admin configures a default or a per-schedule target.
                    _logger.LogWarning(
                        "Maintenance schedule {ScheduleId} is due but has no recipients (per-schedule or default).",
                        schedule.Id);
                    continue;
                }

                await SendForScheduleAsync(
                    schedule,
                    schedule.Equipment.PersianName,
                    today,
                    recipients,
                    cancellationToken);

                while (schedule.NextDueDate <= today)
                {
                    schedule.AdvanceToNextCycle();
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
                sent++;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Maintenance reminder failed for schedule {ScheduleId} — will retry on the next tick.",
                    schedule.Id);
            }
        }

        if (sent > 0)
        {
            _logger.LogInformation("Dispatched {Count} maintenance service reminder(s).", sent);
        }

        return sent;
    }

    /// <summary>Fire-off test (no rolling): sends to every recipient of one schedule.</summary>
    public async Task<MaintenanceReminderTestResult> SendTestAsync(Guid scheduleId, CancellationToken cancellationToken = default)
    {
        var schedule = await _dbContext.MaintenanceSchedules
            .Include(item => item.Equipment)
            .Include(item => item.Recipients)
            .FirstOrDefaultAsync(item => item.Id == scheduleId, cancellationToken)
            ?? throw new KeyNotFoundException("زمان‌بندی سرویس یافت نشد.");

        var defaultRecipients = await LoadDefaultRecipientsAsync(cancellationToken);
        var recipients = ResolveRecipients(schedule, defaultRecipients);
        var usedDefaults = schedule.Recipients.Count == 0 && recipients.Count > 0;

        var result = await SendForScheduleAsync(
            schedule,
            schedule.Equipment.PersianName,
            TehranToday(),
            recipients,
            cancellationToken,
            isTest: true,
            usedDefaults);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return result;
    }

    /// <summary>
    /// Global fallback targets (site admin's mobiles/emails). A schedule's own
    /// list always wins; defaults only fill the gap.
    /// </summary>
    private async Task<List<ReminderTarget>> LoadDefaultRecipientsAsync(CancellationToken cancellationToken)
    {
        var recipients = await _dbContext.MaintenanceDefaultRecipients
            .AsNoTracking()
            .OrderBy(recipient => recipient.Channel)
            .ThenBy(recipient => recipient.Target)
            .ToListAsync(cancellationToken);

        return recipients
            .Select(recipient => new ReminderTarget(recipient.Channel, recipient.Target))
            .ToList();
    }

    private static List<ReminderTarget> ResolveRecipients(
        MaintenanceSchedule schedule,
        IReadOnlyList<ReminderTarget> defaultRecipients)
    {
        if (schedule.Recipients.Count > 0)
        {
            return schedule.Recipients
                .Select(recipient => new ReminderTarget(recipient.Channel, recipient.Target))
                .ToList();
        }

        return defaultRecipients.ToList();
    }

    private async Task<MaintenanceReminderTestResult> SendForScheduleAsync(
        MaintenanceSchedule schedule,
        string equipmentName,
        DateOnly today,
        IReadOnlyList<ReminderTarget> recipients,
        CancellationToken cancellationToken,
        bool isTest = false,
        bool usedDefaults = false)
    {
        var overdueDays = today.DayNumber - schedule.NextDueDate.DayNumber;
        var smsMessage = BuildSmsMessage(equipmentName, schedule.Title, schedule.NextDueDate, overdueDays, isTest);
        var emailSubject = BuildEmailSubject(equipmentName, schedule.Title, overdueDays, isTest);
        var emailBody = BuildEmailBody(equipmentName, schedule.Title, schedule.NextDueDate, overdueDays, isTest);

        var result = new MaintenanceReminderTestResult(schedule.Id, usedDefaults);

        foreach (var recipient in recipients)
        {
            var delivered = false;
            try
            {
                if (recipient.Channel == MaintenanceChannel.Sms)
                {
                    await _smsSender.SendAsync(recipient.Target, smsMessage, cancellationToken);
                }
                else
                {
                    await _emailSender.SendAsync(recipient.Target, emailSubject, emailBody, cancellationToken);
                }

                delivered = true;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Maintenance reminder could not be delivered to {Channel} {Target} (schedule {ScheduleId}).",
                    recipient.Channel,
                    recipient.Target,
                    schedule.Id);
            }

            if (delivered)
            {
                result.Delivered++;
            }
            else
            {
                result.Failures.Add(recipient.Target);
            }
        }

        return result;
    }

    private readonly record struct ReminderTarget(MaintenanceChannel Channel, string Target);

    internal static string BuildSmsMessage(
        string equipmentName,
        string scheduleTitle,
        DateOnly dueDate,
        int overdueDays,
        bool isTest)
    {
        var overdueTag = overdueDays > 0
            ? $" (تأخیر {overdueDays} روز)"
            : string.Empty;
        var testTag = isTest ? " [پیام آزمایشی]" : string.Empty;

        return $"یادآور سرویس دوره‌ای{testTag}: «{equipmentName} — {scheduleTitle}» سررسید {dueDate:yyyy/MM/dd}{overdueTag}. لطفاً اقدام کنید. شوگاریستا";
    }

    internal static string BuildEmailSubject(string equipmentName, string scheduleTitle, int overdueDays, bool isTest)
    {
        var prefix = isTest ? "[آزمایشی] " : string.Empty;
        var overdueTag = overdueDays > 0 ? $" — تأخیر {overdueDays} روز" : string.Empty;

        return $"{prefix}یادآور سرویس دوره‌ای: {equipmentName} — {scheduleTitle}{overdueTag}";
    }

    internal static string BuildEmailBody(
        string equipmentName,
        string scheduleTitle,
        DateOnly dueDate,
        int overdueDays,
        bool isTest)
    {
        var overdueLine = overdueDays > 0
            ? $"<p style=\"color:#b91c1c;font-weight:700;\">این سرویس {overdueDays} روز از موعدش گذشته است.</p>"
            : string.Empty;
        var testLine = isTest
            ? "<p style=\"color:#6b7280;\">این یک پیام آزمایشی است؛ زمان‌بندی جلو نیفتاده و یادآوری واقعی طبق برنامه ارسال می‌شود.</p>"
            : string.Empty;

        return $"""
            <div style="font-family:Tahoma,Segoe UI,sans-serif;direction:rtl;text-align:right;line-height:1.9;">
              <h3 style="margin:0 0 12px;color:#111827;">🔧 یادآور سرویس دوره‌ای — شوگاریستا</h3>
              <p><strong>تجهیز:</strong> {equipmentName}</p>
              <p><strong>کار لازم:</strong> {scheduleTitle}</p>
              <p><strong>تاریخ سررسید:</strong> {dueDate:yyyy/MM/dd}</p>
              {overdueLine}
              {testLine}
              <p style="color:#6b7280;">لطفاً پس از انجام، هزینهٔ سرویس را در پنل مدیریت ثبت کنید.</p>
            </div>
            """;
    }
}

/// <summary>Outcome of a manual test dispatch.</summary>
public sealed record MaintenanceReminderTestResult(
    Guid ScheduleId,
    bool UsedDefaultRecipients = false)
{
    public int Delivered { get; internal set; }

    public List<string> Failures { get; } = [];
}
