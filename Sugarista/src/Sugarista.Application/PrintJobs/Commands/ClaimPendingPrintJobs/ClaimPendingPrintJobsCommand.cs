using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.PrintJobs.Commands.ClaimPendingPrintJobs;

/// <summary>
/// Claims up to <c>maxJobs</c> receipt print jobs for the print agent:
/// returns their payloads and flips them to <see cref="PrintJobStatus.Dispatched"/>.
/// Jobs stuck in Dispatched for too long (agent died mid-print) are reclaimed.
/// </summary>
public record ClaimPendingPrintJobsCommand(int MaxJobs = 20)
    : IRequest<List<PrintJobDispatchDto>>;

public class ClaimPendingPrintJobsCommandValidator : AbstractValidator<ClaimPendingPrintJobsCommand>
{
    public ClaimPendingPrintJobsCommandValidator()
    {
        RuleFor(command => command.MaxJobs).InclusiveBetween(1, 100);
    }
}

public record PrintJobDispatchDto(
    Guid Id,
    string CopyType,
    string OrderTrackingCode,
    string PayloadJson,
    int AttemptCount);

public class ClaimPendingPrintJobsCommandHandler
    : IRequestHandler<ClaimPendingPrintJobsCommand, List<PrintJobDispatchDto>>
{
    /// <summary>How long a Dispatched job stays claimed before it is offered again.</summary>
    private static readonly TimeSpan DispatchRetention = TimeSpan.FromMinutes(2);

    private readonly IApplicationDbContext _dbContext;

    public ClaimPendingPrintJobsCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<PrintJobDispatchDto>> Handle(
        ClaimPendingPrintJobsCommand request,
        CancellationToken cancellationToken)
    {
        var staleCutoff = DateTimeOffset.UtcNow - DispatchRetention;

        var jobs = await _dbContext.PrintJobs
            .Where(job =>
                job.Status == PrintJobStatus.Pending
                || (job.Status == PrintJobStatus.Dispatched && job.DispatchedAt < staleCutoff))
            .OrderBy(job => job.CreatedAt)
            .ThenBy(job => job.Id)
            .Take(request.MaxJobs)
            .ToListAsync(cancellationToken);

        if (jobs.Count == 0)
        {
            return [];
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var job in jobs)
        {
            job.MarkBackToPending();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        foreach (var job in jobs)
        {
            job.MarkDispatched(now);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return jobs
            .Select(job => new PrintJobDispatchDto(
                job.Id,
                job.CopyType.ToString().ToLowerInvariant(),
                job.OrderTrackingCode,
                job.PayloadJson,
                job.AttemptCount))
            .ToList();
    }
}
