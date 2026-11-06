using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Api.Authorization;
using Sugarista.Application.CustomerGroups;
using Sugarista.Application.CustomerSubscriptions;
using Sugarista.Application.SubscriptionPackages;
using Sugarista.Domain.Constants;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Api.Controllers;

/// <summary>
/// Customer groups (تسک ۲۷): create segments with a standing discount, an
/// approval rule (manual or corporate email domain) and a membership validity.
/// Also exposes per-customer subscription management (تسک ۳۰).
/// </summary>
[Authorize]
[Route("api/admin/customer-groups")]
public class AdminCustomerGroupsController : ApiControllerBase
{
    [HttpGet]
    [RequirePermission(AdminPermissions.Orders.View)]
    public async Task<ActionResult<IReadOnlyCollection<AdminCustomerGroupDto>>> Get(
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(new GetAdminCustomerGroupsQuery(), cancellationToken));
    }

    [HttpPost]
    [RequirePermission(AdminPermissions.Orders.Update)]
    public async Task<ActionResult<AdminCustomerGroupDto>> Upsert(
        [FromBody] UpsertCustomerGroupCommand command,
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(command, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(AdminPermissions.Orders.Update)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            await Mediator.Send(new DeleteCustomerGroupCommand(id), cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}

[Authorize]
[Route("api/admin/subscription-packages")]
public class AdminSubscriptionPackagesController : ApiControllerBase
{
    [HttpGet]
    [RequirePermission(AdminPermissions.Orders.View)]
    public async Task<ActionResult<IReadOnlyCollection<AdminSubscriptionPackageDto>>> Get(
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(new GetAdminSubscriptionPackagesQuery(), cancellationToken));
    }

    [HttpPost]
    [RequirePermission(AdminPermissions.Orders.Update)]
    public async Task<ActionResult<AdminSubscriptionPackageDto>> Upsert(
        [FromBody] UpsertSubscriptionPackageCommand command,
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(command, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(AdminPermissions.Orders.Update)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            await Mediator.Send(new DeleteSubscriptionPackageCommand(id), cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (DomainException exception)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "خطای اعتبارسنجی",
                detail: exception.Message);
        }
    }
}

[Authorize]
[Route("api/admin/customers/{customerId:guid}")]
public class AdminCustomerBenefitsController : ApiControllerBase
{
    [HttpGet("groups")]
    [RequirePermission(AdminPermissions.Orders.View)]
    public async Task<ActionResult> GetGroups(
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(new GetCustomerGroupMembershipsQuery(customerId), cancellationToken));
    }

    [HttpPost("groups")]
    [RequirePermission(AdminPermissions.Orders.Update)]
    public async Task<ActionResult> AssignGroup(
        Guid customerId,
        [FromBody] AssignCustomerGroupCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.CustomerId != customerId)
        {
            return BadRequest("Route id does not match payload id.");
        }

        try
        {
            await Mediator.Send(command, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("groups/{groupId:guid}")]
    [RequirePermission(AdminPermissions.Orders.Update)]
    public async Task<ActionResult> RemoveGroup(
        Guid customerId,
        Guid groupId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await Mediator.Send(new RemoveCustomerGroupCommand(customerId, groupId), cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("subscriptions")]
    [RequirePermission(AdminPermissions.Orders.View)]
    public async Task<ActionResult<IReadOnlyCollection<AdminCustomerSubscriptionDto>>> GetSubscriptions(
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(new GetAdminCustomerSubscriptionsQuery(customerId), cancellationToken));
    }

    [HttpPost("subscriptions")]
    [RequirePermission(AdminPermissions.Orders.Update)]
    public async Task<ActionResult<AdminCustomerSubscriptionDto>> Activate(
        Guid customerId,
        [FromBody] ActivateCustomerSubscriptionCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.CustomerId != customerId)
        {
            return BadRequest("Route id does not match payload id.");
        }

        try
        {
            return Ok(await Mediator.Send(command, cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (DomainException exception)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "خطای اعتبارسنجی",
                detail: exception.Message);
        }
    }

    [HttpPost("subscriptions/{subscriptionId:guid}/cancel")]
    [RequirePermission(AdminPermissions.Orders.Update)]
    public async Task<ActionResult<AdminCustomerSubscriptionDto>> Cancel(
        Guid customerId,
        Guid subscriptionId,
        [FromBody] CancelCustomerSubscriptionCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.SubscriptionId != subscriptionId)
        {
            return BadRequest("Route id does not match payload id.");
        }

        try
        {
            return Ok(await Mediator.Send(command, cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (DomainException exception)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "خطای اعتبارسنجی",
                detail: exception.Message);
        }
    }
}
