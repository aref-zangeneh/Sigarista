using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Application.Affiliates.Portal;
using Sugarista.Application.Customers.Auth.Queries.GetCustomerAuthState;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Api.Controllers;

public sealed record ApplyAffiliateRequest(
    string ShebaNumber,
    string? CardNumber,
    string? PreferredSlug);

public sealed record CreateAffiliateCampaignLinkRequest(
    string SubId,
    string? TargetUrl);

public sealed record AffiliateAmountRequest(decimal Amount);

public sealed record UpdateAffiliateBankInfoRequest(
    string ShebaNumber,
    string? CardNumber);

/// <summary>
/// Customer-facing Affiliate Portal API (authenticated via the existing
/// sg_customer_session HttpOnly OTP cookie).
/// </summary>
[AllowAnonymous]
[Route("api/affiliate-portal")]
public class AffiliatePortalController : ApiControllerBase
{
    private readonly IAffiliatePortalService _portalService;

    public AffiliatePortalController(IAffiliatePortalService portalService)
    {
        _portalService = portalService;
    }

    [HttpGet("me")]
    public async Task<ActionResult<AffiliatePortalDashboardDto>> GetMe(CancellationToken cancellationToken = default)
    {
        var customerId = await ResolveAuthenticatedCustomerIdAsync(cancellationToken);
        if (!customerId.HasValue)
        {
            return Ok(AffiliatePortalService.EmptyUnauthenticated());
        }

        var origin = $"{Request.Scheme}://{Request.Host}";
        return Ok(await _portalService.GetDashboardAsync(customerId.Value, origin, cancellationToken));
    }

    [HttpPost("apply")]
    public async Task<ActionResult<AffiliatePortalDashboardDto>> Apply(
        [FromBody] ApplyAffiliateRequest request,
        CancellationToken cancellationToken = default)
    {
        var customerId = await ResolveAuthenticatedCustomerIdAsync(cancellationToken);
        if (!customerId.HasValue)
        {
            return Unauthorized("لطفاً ابتدا شماره موبایل خود را با کد تایید (OTP) احراز هویت کنید.");
        }

        try
        {
            var origin = $"{Request.Scheme}://{Request.Host}";
            return Ok(await _portalService.ApplyAsync(
                customerId.Value,
                request.ShebaNumber,
                request.CardNumber,
                request.PreferredSlug,
                origin,
                cancellationToken));
        }
        catch (DomainException exception)
        {
            return Problem(exception.Message, statusCode: 400);
        }
    }

    [HttpPost("links")]
    public async Task<ActionResult<AffiliatePortalLinkDto>> CreateCampaignLink(
        [FromBody] CreateAffiliateCampaignLinkRequest request,
        CancellationToken cancellationToken = default)
    {
        var customerId = await ResolveAuthenticatedCustomerIdAsync(cancellationToken);
        if (!customerId.HasValue)
        {
            return Unauthorized();
        }

        try
        {
            var origin = $"{Request.Scheme}://{Request.Host}";
            return Ok(await _portalService.CreateCampaignLinkAsync(
                customerId.Value,
                request.SubId,
                request.TargetUrl,
                origin,
                cancellationToken));
        }
        catch (DomainException exception)
        {
            return Problem(exception.Message, statusCode: 400);
        }
    }

    [HttpPost("convert-to-credit")]
    public async Task<ActionResult<ConvertCashToCreditResultDto>> ConvertToCredit(
        [FromBody] AffiliateAmountRequest request,
        CancellationToken cancellationToken = default)
    {
        var customerId = await ResolveAuthenticatedCustomerIdAsync(cancellationToken);
        if (!customerId.HasValue)
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await _portalService.ConvertCashToCafeCreditAsync(
                customerId.Value,
                request.Amount,
                cancellationToken));
        }
        catch (DomainException exception)
        {
            return Problem(exception.Message, statusCode: 400);
        }
    }

    [HttpPost("payout-request")]
    public async Task<ActionResult<AffiliatePortalPayoutDto>> RequestPayout(
        [FromBody] AffiliateAmountRequest request,
        CancellationToken cancellationToken = default)
    {
        var customerId = await ResolveAuthenticatedCustomerIdAsync(cancellationToken);
        if (!customerId.HasValue)
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await _portalService.RequestCashPayoutAsync(
                customerId.Value,
                request.Amount,
                cancellationToken));
        }
        catch (DomainException exception)
        {
            return Problem(exception.Message, statusCode: 400);
        }
    }

    [HttpPut("bank-info")]
    public async Task<ActionResult> UpdateBankInfo(
        [FromBody] UpdateAffiliateBankInfoRequest request,
        CancellationToken cancellationToken = default)
    {
        var customerId = await ResolveAuthenticatedCustomerIdAsync(cancellationToken);
        if (!customerId.HasValue)
        {
            return Unauthorized();
        }

        try
        {
            await _portalService.UpdateBankInfoAsync(
                customerId.Value,
                request.ShebaNumber,
                request.CardNumber,
                cancellationToken);
            return NoContent();
        }
        catch (DomainException exception)
        {
            return Problem(exception.Message, statusCode: 400);
        }
    }

    private async Task<Guid?> ResolveAuthenticatedCustomerIdAsync(CancellationToken cancellationToken)
    {
        if (!Request.Cookies.TryGetValue(CustomerAuthController.SessionCookieName, out var token) ||
            string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var authState = await Mediator.Send(new GetCustomerAuthStateQuery(token), cancellationToken);
        return authState?.CustomerId;
    }
}
