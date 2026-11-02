using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.PrintJobs.Commands.MarkPrintJobFailed;

public record MarkPrintJobFailedCommand(Guid PrintJobId, string? Error)
    : IRequest<MarkPrintJobFailedResultDto>;

public record MarkPrintJobFailedResultDto(Guid Id, string Status, int AttemptCount);

public class MarkPrintJobFailedCommandValidator : AbstractValidator<MarkPrintJobFailedCommand>
{
    public MarkPrintJobFailedCommandValidator()
    {
        RuleFor(command => command.PrintJobId).NotEmpty();
        RuleFor(command => command.Error).MaximumLength(2000);
    }
}

public class MarkPrintJobFailedCommandHandler
    : IRequestHandler<MarkPrintJobFailedCommand, MarkPrintJobFailedResultDto>
{
    private readonly IApplicationDbContext _dbContext;

    public MarkPrintJobFailedCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<MarkPrintJobFailedResultDto> Handle(
        MarkPrintJobFailedCommand request,
        CancellationToken cancellationToken)
    {
        var job = await _dbContext.PrintJobs
            .FirstOrDefaultAsync(job => job.Id == request.PrintJobId, cancellationToken)
            ?? throw new KeyNotFoundException("Print job was not found.");

        job.MarkFailed(request.Error);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new MarkPrintJobFailedResultDto(job.Id, job.Status.ToString(), job.AttemptCount);
    }
}
