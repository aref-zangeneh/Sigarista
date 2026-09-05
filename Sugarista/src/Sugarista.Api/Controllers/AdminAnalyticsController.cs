using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Api.Authorization;
using Sugarista.Application.Analytics.Queries.GetAdminDashboard;
using Sugarista.Domain.Constants;

namespace Sugarista.Api.Controllers;

[Authorize]
[Route("api/admin/analytics")]
public class AdminAnalyticsController : ApiControllerBase
{
    [HttpGet("dashboard")]
    [RequirePermission(AdminPermissions.Dashboard.View)]
    public async Task<ActionResult<AdminDashboardDto>> GetDashboard(CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new GetAdminDashboardQuery(), cancellationToken));
    }
}
