using Microsoft.EntityFrameworkCore;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Hr.OpenHours.GetCafeOpenHours;

public record CafeOpenHourDto(
    int Weekday,
    string WeekdayLabel,
    bool IsOpen,
    string OpenTime,
    string CloseTime,
    decimal ScheduledMinutes);

public record GetCafeOpenHoursQuery : IRequest<IReadOnlyList<CafeOpenHourDto>>;

/// <summary>
/// Returns the 7 weekday rows always (creating defaults when the table is
/// empty), so the UI can render a complete editable week.
/// </summary>
public class GetCafeOpenHoursQueryHandler : IRequestHandler<GetCafeOpenHoursQuery, IReadOnlyList<CafeOpenHourDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetCafeOpenHoursQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<CafeOpenHourDto>> Handle(
        GetCafeOpenHoursQuery request,
        CancellationToken cancellationToken)
    {
        var rows = await _dbContext.CafeOpenHours
            .OrderBy(hour => hour.Weekday)
            .ToListAsync(cancellationToken);

        var added = false;
        for (var weekday = 0; weekday <= 6; weekday++)
        {
            if (rows.Exists(row => row.Weekday == weekday))
            {
                continue;
            }

            // sensible default: open 09:00–22:00, Friday closed (بازار تعطیل)
            var row = new CafeOpenHour(
                weekday,
                isOpen: weekday != (int)DayOfWeek.Friday,
                new TimeOnly(9, 0),
                new TimeOnly(22, 0));
            _dbContext.CafeOpenHours.Add(row);
            rows.Add(row);
            added = true;
        }

        if (added)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            rows = rows.OrderBy(hour => hour.Weekday).ToList();
        }

        return rows.Select(hour => new CafeOpenHourDto(
            hour.Weekday,
            HrShamsiCalendar.WeekdayLabel(hour.Weekday),
            hour.IsOpen,
            hour.OpenTime.ToString("HH:mm"),
            hour.CloseTime.ToString("HH:mm"),
            hour.ScheduledMinutes)).ToList();
    }
}
