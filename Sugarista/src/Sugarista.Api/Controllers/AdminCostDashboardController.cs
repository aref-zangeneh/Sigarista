using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Api.Authorization;
using Sugarista.Application.CostControl.Commands.SaveCostControlState;
using Sugarista.Application.CostControl.Queries.GetCostDashboard;
using Sugarista.Domain.Constants;

namespace Sugarista.Api.Controllers;

[Authorize]
[Route("api/admin/cost-dashboard")]
public class AdminCostDashboardController : ApiControllerBase
{
    [HttpGet("bundle")]
    [RequirePermission(AdminPermissions.Dashboard.View)]
    public async Task<ActionResult<CostDashboardBundleDto>> GetBundle(CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new GetCostDashboardQuery(), cancellationToken));
    }

    [HttpPut("state")]
    [RequirePermission(AdminPermissions.Dashboard.Update)]
    public async Task<ActionResult<SaveCostControlStateResultDto>> SaveState(
        [FromBody] SaveCostControlStateCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command, cancellationToken));
    }
}
