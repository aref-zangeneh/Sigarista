using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.PrintJobs.Commands.MarkPrintJobPrinted;

public record MarkPrintJobPrintedCommand(Guid PrintJobId)
    : IRequest<MarkPrintJobPrintedResultDto>;

public record MarkPrintJobPrintedResultDto(Guid Id, string Status);

public class MarkPrintJobPrintedCommandValidator : AbstractValidator<MarkPrintJobPrintedCommand>
{
    public MarkPrintJobPrintedCommandValidator()
    {
        RuleFor(command => command.PrintJobId).NotEmpty();
    }
}

public class MarkPrintJobPrintedCommandHandler
    : IRequestHandler<MarkPrintJobPrintedCommand, MarkPrintJobPrintedResultDto>
{
    private readonly IApplicationDbContext _dbContext;

    public MarkPrintJobPrintedCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<MarkPrintJobPrintedResultDto> Handle(
        MarkPrintJobPrintedCommand request,
        CancellationToken cancellationToken)
    {
        var job = await _dbContext.PrintJobs
            .FirstOrDefaultAsync(job => job.Id == request.PrintJobId, cancellationToken)
            ?? throw new KeyNotFoundException("Print job was not found.");

        job.MarkPrinted();
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new MarkPrintJobPrintedResultDto(job.Id, job.Status.ToString());
    }
}
