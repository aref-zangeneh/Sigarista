using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.Discounts.Reports;

/// <summary>
/// Sends scheduled discount reports: builds the Excel workbook for the period
/// that just finished, e-mails it to every recipient and records the run. Used
/// by the background ticker and by the admin "send now" button, so both paths
/// behave exactly the same.
/// </summary>
public sealed class ReportScheduleDispatcher
{
    private readonly IApplicationDbContext _dbContext;
    private readonly DiscountReportBuilder _builder;
    private readonly IMaintenanceEmailSender _emailSender;
    private readonly ILogger<ReportScheduleDispatcher> _logger;

    public ReportScheduleDispatcher(
        IApplicationDbContext dbContext,
        DiscountReportBuilder builder,
        IMaintenanceEmailSender emailSender,
        ILogger<ReportScheduleDispatcher> logger)
    {
        _dbContext = dbContext;
        _builder = builder;
        _emailSender = emailSender;
        _logger = logger;
    }

    /// <summary>Fires every schedule that is due now. Returns how many were sent.</summary>
    public async Task<int> DispatchDueAsync(CancellationToken cancellationToken = default)
    {
        var timeZone = await ResolveTimeZoneAsync(cancellationToken);
        var localNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone);

        var schedules = await _dbContext.ReportSchedules
            .Where(schedule => schedule.IsEnabled)
            .ToListAsync(cancellationToken);

        var sent = 0;

        foreach (var schedule in schedules)
        {
            if (!schedule.IsDue(localNow))
            {
                continue;
            }

            try
            {
                await SendAsync(schedule, localNow, timeZone, cancellationToken);
                sent++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Scheduled discount report {ScheduleId} ({Name}) could not be sent.",
                    schedule.Id,
                    schedule.Name);
            }
        }

        return sent;
    }

    /// <summary>Sends a schedule immediately (admin "send now"), regardless of its period.</summary>
    public async Task SendNowAsync(Guid scheduleId, CancellationToken cancellationToken = default)
    {
        var schedule = await _dbContext.ReportSchedules
            .FirstOrDefaultAsync(entry => entry.Id == scheduleId, cancellationToken)
            ?? throw new KeyNotFoundException("Report schedule was not found.");

        var timeZone = await ResolveTimeZoneAsync(cancellationToken);
        var localNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone);
        await SendAsync(schedule, localNow, timeZone, cancellationToken);
    }

    private async Task SendAsync(
        ReportSchedule schedule,
        DateTimeOffset localNow,
        TimeZoneInfo timeZone,
        CancellationToken cancellationToken)
    {
        var recipients = schedule.Recipients();
        if (recipients.Count == 0)
        {
            throw new DomainException("هیچ ایمیل دریافت‌کننده‌ای برای این گزارش تنظیم نشده است.");
        }

        var options = DiscountReportScheduleOptionsSerializer.Deserialize(schedule.OptionsJson);
        var (from, to) = ResolvePeriod(schedule.Frequency, localNow, timeZone);

        var filter = new DiscountReportFilter(
            from,
            to,
            options.DiscountId,
            options.CategoryId,
            options.OnlyUnscopedCategories,
            options.Kind,
            options.Channel,
            options.Code,
            options.Columns);

        var report = await _builder.BuildAsync(filter, cancellationToken);
        var file = await _builder.BuildExcelAsync(filter, cancellationToken);

        var subject = $"گزارش عملکرد تخفیف‌ها — {schedule.Name}";
        var body = BuildEmailBody(schedule, report);

        var attachment = new EmailAttachment(
            file.FileName,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            file.Content);

        foreach (var recipient in recipients)
        {
            await _emailSender.SendAsync(
                recipient,
                subject,
                body,
                [attachment],
                cancellationToken);
        }

        schedule.MarkRun(DateTimeOffset.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Discount report {ScheduleId} sent to {RecipientCount} recipient(s) for {From:yyyy-MM-dd}..{To:yyyy-MM-dd}.",
            schedule.Id,
            recipients.Count,
            from,
            to);
    }

    private static (DateTimeOffset From, DateTimeOffset To) ResolvePeriod(
        ReportFrequency frequency,
        DateTimeOffset localNow,
        TimeZoneInfo timeZone)
    {
        var today = new DateTime(localNow.Year, localNow.Month, localNow.Day, 0, 0, 0, DateTimeKind.Unspecified);

        var (startLocal, endLocal) = frequency switch
        {
            ReportFrequency.Weekly => (today.AddDays(-6), today),
            ReportFrequency.Monthly => (new DateTime(today.Year, today.Month, 1).AddMonths(-1), new DateTime(today.Year, today.Month, 1)),
            _ => (today.AddDays(-1), today),
        };

        var startUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, timeZone);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(endLocal, timeZone);
        return (new DateTimeOffset(startUtc, TimeSpan.Zero), new DateTimeOffset(endUtc, TimeSpan.Zero));
    }

    private static string BuildEmailBody(ReportSchedule schedule, DiscountReportResult report)
    {
        var topRows = string.Join(
            string.Empty,
            report.Summary.Take(10).Select(row =>
                $"<tr><td style=\"padding:6px 10px;border-bottom:1px solid #eee\">{row.Name}</td>" +
                $"<td style=\"padding:6px 10px;border-bottom:1px solid #eee\">{DiscountReportBuilder.KindTitle(row.Kind)}</td>" +
                $"<td style=\"padding:6px 10px;border-bottom:1px solid #eee;direction:ltr\">{row.Amount:N0}</td></tr>"));

        return $"""
            <div dir="rtl" style="font-family:Tahoma,Arial,sans-serif;color:#22333b;line-height:1.9">
              <h2 style="margin:0 0 6px">گزارش عملکرد تخفیف‌ها</h2>
              <p style="margin:0 0 16px;color:#5b6b73">{schedule.Name} — از {report.From:yyyy-MM-dd} تا {report.To:yyyy-MM-dd}</p>
              <table style="border-collapse:collapse;margin-bottom:16px">
                <tr><td style="padding:6px 10px">مجموع تخفیف</td><td style="padding:6px 10px;font-weight:bold;direction:ltr">{report.TotalDiscount:N0} تومان</td></tr>
                <tr><td style="padding:6px 10px">تعداد استفاده</td><td style="padding:6px 10px;font-weight:bold">{report.TotalUsage:N0}</td></tr>
                <tr><td style="padding:6px 10px">تعداد سفارش</td><td style="padding:6px 10px;font-weight:bold">{report.TotalOrders:N0}</td></tr>
                <tr><td style="padding:6px 10px">تعداد مشتری</td><td style="padding:6px 10px;font-weight:bold">{report.TotalCustomers:N0}</td></tr>
              </table>
              <h3 style="margin:0 0 8px">بیشترین تخفیف‌ها</h3>
              <table style="border-collapse:collapse;min-width:420px">
                <thead>
                  <tr style="background:#f0f6fa">
                    <th style="padding:8px 10px;text-align:right">تخفیف</th>
                    <th style="padding:8px 10px;text-align:right">نوع</th>
                    <th style="padding:8px 10px;text-align:right">مبلغ (تومان)</th>
                  </tr>
                </thead>
                <tbody>{topRows}</tbody>
              </table>
              <p style="margin-top:18px;color:#7b8a91;font-size:12px">فایل کامل Excel این گزارش به ایمیل پیوست شده است.</p>
            </div>
            """;
    }

    private async Task<TimeZoneInfo> ResolveTimeZoneAsync(CancellationToken cancellationToken)
    {
        var timeZoneId = await _dbContext.SiteSettings
            .AsNoTracking()
            .Select(settings => settings.TimeZoneId)
            .FirstOrDefaultAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(timeZoneId))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");
    }
}

/// <summary>Admin "send now" action.</summary>
public record SendReportScheduleNowCommand(Guid Id) : IRequest;

public class SendReportScheduleNowCommandHandler : IRequestHandler<SendReportScheduleNowCommand>
{
    private readonly ReportScheduleDispatcher _dispatcher;

    public SendReportScheduleNowCommandHandler(ReportScheduleDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public Task Handle(SendReportScheduleNowCommand request, CancellationToken cancellationToken) =>
        _dispatcher.SendNowAsync(request.Id, cancellationToken);
}
