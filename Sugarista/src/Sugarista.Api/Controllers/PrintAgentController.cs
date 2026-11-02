using MediatR;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Application.PrintJobs.Commands.ClaimPendingPrintJobs;
using Sugarista.Application.PrintJobs.Commands.MarkPrintJobFailed;
using Sugarista.Application.PrintJobs.Commands.MarkPrintJobPrinted;

namespace Sugarista.Api.Controllers;

/// <summary>
/// Endpoints consumed by the Sugarista print agent running on the store's
/// laptop (next to the receipt printer). Gated by the shared agent key.
/// </summary>
[ApiController]
[Route("api/print-agent")]
public class PrintAgentController : ControllerBase
{
    private readonly IMediator _mediator;

    public PrintAgentController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Claims pending receipt print jobs (also reclaims stale dispatches).</summary>
    [HttpGet("pending")]
    public async Task<ActionResult<List<PrintJobDispatchDto>>> GetPending(
        [FromQuery] int maxJobs = 20,
        CancellationToken cancellationToken = default)
    {
        var jobs = await _mediator.Send(new ClaimPendingPrintJobsCommand(maxJobs), cancellationToken);
        return Ok(jobs);
    }

    /// <summary>Printer accepted the copy — confirm the job.</summary>
    [HttpPost("{id:guid}/printed")]
    public async Task<ActionResult<MarkPrintJobPrintedResultDto>> MarkPrinted(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(new MarkPrintJobPrintedCommand(id), cancellationToken));
    }

    /// <summary>Printer rejected or errored after all agent retries.</summary>
    [HttpPost("{id:guid}/failed")]
    public async Task<ActionResult<MarkPrintJobFailedResultDto>> MarkFailed(
        Guid id,
        [FromBody] MarkPrintJobFailedRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(
            new MarkPrintJobFailedCommand(id, request?.Error),
            cancellationToken));
    }

    public record MarkPrintJobFailedRequest(string? Error);
}
