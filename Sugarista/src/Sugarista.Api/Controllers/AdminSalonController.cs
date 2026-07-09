using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Application.Salon.Commands.DeleteSalonTable;
using Sugarista.Application.Salon.Commands.OccupySalonTable;
using Sugarista.Application.Salon.Commands.ReleaseSalonTable;
using Sugarista.Application.Salon.Commands.UpsertSalonTable;
using Sugarista.Application.Salon.Queries.GetSalonStatus;
using Sugarista.Domain.Constants;

namespace Sugarista.Api.Controllers;

[Authorize(Roles = RoleNames.Admin)]
[Route("api/admin/salon")]
public class AdminSalonController : ApiControllerBase
{
    [HttpGet("status")]
    public async Task<ActionResult<SalonStatusDto>> GetStatus(CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new GetSalonStatusQuery(), cancellationToken));
    }

    [HttpGet("tables")]
    public async Task<ActionResult<IReadOnlyCollection<SalonTableStatusDto>>> GetTables(CancellationToken cancellationToken)
    {
        var status = await Mediator.Send(new GetSalonStatusQuery(), cancellationToken);
        return Ok(status.Tables);
    }

    [HttpPost("tables")]
    public async Task<ActionResult<Guid>> CreateTable(
        [FromBody] UpsertSalonTableCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command with { Id = null }, cancellationToken));
    }

    [HttpPut("tables/{id:guid}")]
    public async Task<ActionResult<Guid>> UpdateTable(
        Guid id,
        [FromBody] UpsertSalonTableCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command with { Id = id }, cancellationToken));
    }

    [HttpDelete("tables/{id:guid}")]
    public async Task<IActionResult> DeleteTable(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteSalonTableCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("tables/{id:guid}/occupy")]
    public async Task<ActionResult<Guid>> OccupyTable(
        Guid id,
        [FromBody] OccupySalonTableRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(
            new OccupySalonTableCommand(id, request.PeopleCount, request.Note),
            cancellationToken));
    }

    [HttpPost("tables/{id:guid}/release")]
    public async Task<IActionResult> ReleaseTable(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new ReleaseSalonTableCommand(id), cancellationToken);
        return NoContent();
    }
}

public record OccupySalonTableRequest(int PeopleCount, string? Note);
