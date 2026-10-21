using Microsoft.EntityFrameworkCore;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.Hr.OpenHours.UpdateCafeOpenHours;

/// <summary>Updates every weekday row in one atomic save.</summary>
public record CafeOpenHourInput(
    int Weekday,
    bool IsOpen,
    string OpenTime,
    string CloseTime);

public record UpdateCafeOpenHoursCommand(IReadOnlyList<CafeOpenHourInput> Days) : IRequest<IReadOnlyList<Sugarista.Application.Hr.OpenHours.GetCafeOpenHours.CafeOpenHourDto>>;

public class UpdateCafeOpenHoursCommandHandler
    : IRequestHandler<UpdateCafeOpenHoursCommand, IReadOnlyList<Sugarista.Application.Hr.OpenHours.GetCafeOpenHours.CafeOpenHourDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public UpdateCafeOpenHoursCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Sugarista.Application.Hr.OpenHours.GetCafeOpenHours.CafeOpenHourDto>> Handle(
        UpdateCafeOpenHoursCommand request,
        CancellationToken cancellationToken)
    {
        var rows = await _dbContext.CafeOpenHours.ToListAsync(cancellationToken);

        foreach (var input in request.Days.DistinctBy(day => day.Weekday))
        {
            if (!TimeOnly.TryParse(input.OpenTime, out var openTime)
                || !TimeOnly.TryParse(input.CloseTime, out var closeTime))
            {
                throw new DomainException("فرمت ساعت باید HH:mm باشد.");
            }

            var row = rows.FirstOrDefault(existing => existing.Weekday == input.Weekday)
                ?? new CafeOpenHour(input.Weekday, input.IsOpen, openTime, closeTime);

            if (rows.All(existing => existing.Weekday != input.Weekday))
            {
                _dbContext.CafeOpenHours.Add(row);
                rows.Add(row);
            }
            else
            {
                row.Update(input.IsOpen, openTime, closeTime);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await new GetCafeOpenHours.GetCafeOpenHoursQueryHandler(_dbContext)
            .Handle(new GetCafeOpenHours.GetCafeOpenHoursQuery(), cancellationToken);
    }
}
