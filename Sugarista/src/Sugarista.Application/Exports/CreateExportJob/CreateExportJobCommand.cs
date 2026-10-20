using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Exports.CreateExportJob;

/// <summary>
/// Enqueues an async Excel export job. The request returns immediately —
/// the workbook is built in the background and downloaded afterwards,
/// so large datasets can never time out the HTTP request.
/// </summary>
public record CreateExportJobCommand(string Kind, DateOnly? From = null, DateOnly? To = null)
    : IRequest<ExportJobDto>;

public record ExportJobDto(
    Guid Id,
    string Kind,
    DateOnly? FromDate,
    DateOnly? ToDate,
    string Status,
    string FileName,
    string? ErrorMessage,
    long RowsCount,
    long FileSizeBytes,
    string RequestedBy,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset CreatedAt);

public class CreateExportJobCommandValidator : AbstractValidator<CreateExportJobCommand>
{
    public CreateExportJobCommandValidator()
    {
        RuleFor(command => command.Kind)
            .NotEmpty()
            .Must(kind => ExportSectionKinds.KnownKinds.Contains((kind ?? string.Empty).Trim().ToLowerInvariant()))
            .WithMessage("Export kind is not supported.");

        RuleFor(command => command)
            .Must(command => !command.From.HasValue || !command.To.HasValue || command.From <= command.To)
            .WithMessage("From date must be before the to date.");
    }
}

public class CreateExportJobCommandHandler : IRequestHandler<CreateExportJobCommand, ExportJobDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IExportJobQueue _queue;
    private readonly ICurrentUserService _currentUserService;

    public CreateExportJobCommandHandler(
        IApplicationDbContext dbContext,
        IExportJobQueue queue,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _queue = queue;
        _currentUserService = currentUserService;
    }

    public async Task<ExportJobDto> Handle(
        CreateExportJobCommand request,
        CancellationToken cancellationToken)
    {
        var kind = request.Kind.Trim().ToLowerInvariant();
        var requestedBy = _currentUserService.UserId ?? "unknown";

        var job = new ExportJob(kind, request.From, request.To, requestedBy);
        _dbContext.ExportJobs.Add(job);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _queue.EnqueueAsync(job.Id, cancellationToken);

        return ExportJobMapper.ToDto(job);
    }
}

internal static class ExportJobMapper
{
    internal static ExportJobDto ToDto(ExportJob job) => new(
        job.Id,
        job.Kind,
        job.FromDate,
        job.ToDate,
        job.Status.ToString(),
        job.FileName ?? string.Empty,
        job.ErrorMessage,
        job.RowsCount,
        job.FileSizeBytes,
        job.RequestedBy,
        job.StartedAt,
        job.CompletedAt,
        job.CreatedAt);
}
