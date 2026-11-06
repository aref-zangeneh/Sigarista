using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.Discounts.Reports;

/// <summary>Serialized options of a scheduled discount report.</summary>
public sealed record DiscountReportScheduleOptions(
    IReadOnlyCollection<string>? Columns = null,
    Guid? DiscountId = null,
    Guid? CategoryId = null,
    bool OnlyUnscopedCategories = false,
    string? Kind = null,
    string? Channel = null,
    string? Code = null);

public static class DiscountReportScheduleOptionsSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static DiscountReportScheduleOptions Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new DiscountReportScheduleOptions();
        }

        try
        {
            return JsonSerializer.Deserialize<DiscountReportScheduleOptions>(json, Options)
                   ?? new DiscountReportScheduleOptions();
        }
        catch (JsonException)
        {
            return new DiscountReportScheduleOptions();
        }
    }

    public static string Serialize(DiscountReportScheduleOptions options) =>
        JsonSerializer.Serialize(options, Options);
}

public sealed record ReportScheduleDto(
    Guid Id,
    string Name,
    string Kind,
    string Frequency,
    int Hour,
    int? DayOfWeek,
    int? DayOfMonth,
    string RecipientEmails,
    IReadOnlyCollection<string> Recipients,
    string? OptionsJson,
    bool IsEnabled,
    DateTimeOffset? LastRunAt);

public record GetReportSchedulesQuery : IRequest<IReadOnlyCollection<ReportScheduleDto>>;

public class GetReportSchedulesQueryHandler
    : IRequestHandler<GetReportSchedulesQuery, IReadOnlyCollection<ReportScheduleDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetReportSchedulesQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<ReportScheduleDto>> Handle(
        GetReportSchedulesQuery request,
        CancellationToken cancellationToken)
    {
        var schedules = await _dbContext.ReportSchedules
            .AsNoTracking()
            .OrderByDescending(schedule => schedule.IsEnabled)
            .ThenBy(schedule => schedule.Name)
            .ToListAsync(cancellationToken);

        return schedules
            .Select(schedule => new ReportScheduleDto(
                schedule.Id,
                schedule.Name,
                schedule.Kind,
                schedule.Frequency.ToString(),
                schedule.Hour,
                schedule.DayOfWeek,
                schedule.DayOfMonth,
                schedule.RecipientEmails,
                schedule.Recipients(),
                schedule.OptionsJson,
                schedule.IsEnabled,
                schedule.LastRunAt))
            .ToList();
    }
}

public record UpsertReportScheduleCommand(
    Guid? Id,
    string Name,
    string Kind,
    string Frequency,
    int Hour,
    string RecipientEmails,
    int? DayOfWeek = null,
    int? DayOfMonth = null,
    string? OptionsJson = null,
    bool IsEnabled = true) : IRequest<Guid>;

public class UpsertReportScheduleCommandValidator : AbstractValidator<UpsertReportScheduleCommand>
{
    public UpsertReportScheduleCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(150);
        RuleFor(command => command.Kind)
            .Must(kind => string.Equals(kind, "discount-report", StringComparison.OrdinalIgnoreCase))
            .WithMessage("نوع گزارش پشتیبانی نمی‌شود.");
        RuleFor(command => command.Frequency)
            .Must(frequency => Enum.TryParse<ReportFrequency>(frequency, true, out _))
            .WithMessage("دورهٔ ارسال باید Daily، Weekly یا Monthly باشد.");
        RuleFor(command => command.Hour).InclusiveBetween(0, 23);
        RuleFor(command => command.DayOfWeek).InclusiveBetween(0, 6)
            .When(command => command.DayOfWeek.HasValue);
        RuleFor(command => command.DayOfMonth).InclusiveBetween(1, 31)
            .When(command => command.DayOfMonth.HasValue);
        RuleFor(command => command.RecipientEmails)
            .NotEmpty()
            .MaximumLength(1000)
            .Must(emails => emails
                .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .All(email => email.Contains('@', StringComparison.Ordinal) && email.Length >= 5))
            .WithMessage("ایمیل دریافت‌کننده معتبر نیست.");
        RuleFor(command => command.OptionsJson).MaximumLength(8000);
    }
}

public class UpsertReportScheduleCommandHandler : IRequestHandler<UpsertReportScheduleCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;

    public UpsertReportScheduleCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Guid> Handle(
        UpsertReportScheduleCommand request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<ReportFrequency>(request.Frequency, true, out var frequency))
        {
            throw new DomainException("دورهٔ ارسال گزارش معتبر نیست.");
        }

        ReportSchedule schedule;

        if (request.Id is { } id)
        {
            schedule = await _dbContext.ReportSchedules
                .FirstOrDefaultAsync(entry => entry.Id == id, cancellationToken)
                ?? throw new KeyNotFoundException("Report schedule was not found.");

            schedule.Update(
                request.Name,
                request.Kind,
                frequency,
                request.Hour,
                request.RecipientEmails,
                request.OptionsJson,
                request.DayOfWeek,
                request.DayOfMonth,
                request.IsEnabled);
        }
        else
        {
            schedule = new ReportSchedule(
                request.Name,
                request.Kind,
                frequency,
                request.Hour,
                request.RecipientEmails,
                request.OptionsJson,
                request.DayOfWeek,
                request.DayOfMonth,
                request.IsEnabled);

            _dbContext.ReportSchedules.Add(schedule);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return schedule.Id;
    }
}

public record DeleteReportScheduleCommand(Guid Id) : IRequest;

public class DeleteReportScheduleCommandHandler : IRequestHandler<DeleteReportScheduleCommand>
{
    private readonly IApplicationDbContext _dbContext;

    public DeleteReportScheduleCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(DeleteReportScheduleCommand request, CancellationToken cancellationToken)
    {
        var schedule = await _dbContext.ReportSchedules
            .FirstOrDefaultAsync(entry => entry.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Report schedule was not found.");

        _dbContext.ReportSchedules.Remove(schedule);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
