using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sugarista.Api.Authorization;
using Sugarista.Application.Affiliates.Services;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Pos;
using Sugarista.Application.Pos.Commands.CancelPosOrder;
using Sugarista.Application.Pos.Commands.CopyPosOrder;
using Sugarista.Application.Pos.Commands.UpdatePosOrderManagerNote;
using Sugarista.Application.Pos.Commands.UpsertPosOrder;
using Sugarista.Application.Pos.Queries.GetPosBootstrap;
using Sugarista.Application.Pos.Queries.GetPosCustomerByPhone;
using Sugarista.Application.Pos.Queries.GetPosOrder;
using Sugarista.Domain.Constants;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Api.Controllers;

public sealed record UpdateManagerNoteBody(string? ManagerNote);

public sealed record PosCustomerSearchItemDto(
    Guid Id,
    string PersianName,
    string PhoneNumber,
    decimal LoyaltyPoints,
    string? MembershipTierName);

[Authorize]
[Route("api/admin/pos")]
public class AdminPosController : ApiControllerBase
{
    private readonly IAffiliateTrackingService _affiliateTrackingService;
    private readonly IApplicationDbContext _dbContext;

    public AdminPosController(
        IAffiliateTrackingService affiliateTrackingService,
        IApplicationDbContext dbContext)
    {
        _affiliateTrackingService = affiliateTrackingService;
        _dbContext = dbContext;
    }

    [HttpGet("affiliate-coupon")]
    [RequirePermission(AdminPermissions.Pos.View)]
    public async Task<ActionResult<AffiliateCouponResolution>> PreviewAffiliateCoupon(
        [FromQuery] string? code,
        [FromQuery] decimal subtotal = 0m,
        [FromQuery] Guid? customerId = null,
        [FromQuery] string? customerPhone = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return BadRequest("کد معرف وارد نشده است.");
        }

        try
        {
            var resolution = await _affiliateTrackingService.TryResolveCouponAsync(
                code,
                Math.Max(0m, subtotal),
                customerId,
                throwOnInvalidCoupon: true,
                cancellationToken,
                customerPhone: customerPhone);

            if (resolution is null)
            {
                return NotFound("کد معرف با این مشخصات یافت نشد.");
            }

            return Ok(resolution);
        }
        catch (DomainException exception)
        {
            return Problem(exception.Message);
        }
    }
    [HttpGet("bootstrap")]
    [RequirePermission(AdminPermissions.Pos.View)]
    public async Task<ActionResult<PosBootstrapDto>> GetBootstrap(CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new GetPosBootstrapQuery(), cancellationToken));
    }

    [HttpGet("customers/lookup")]
    [RequirePermission(AdminPermissions.Pos.View)]
    public async Task<ActionResult<PosCustomerLookupDto>> LookupCustomer(
        [FromQuery] string? phone,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new GetPosCustomerByPhoneQuery(phone), cancellationToken));
    }

    /// <summary>
    /// Fast autocomplete customer search for POS cashiers:
    /// - By phone prefix once at least 5 digits are entered (e.g. "09123" -> matching customers starting with 09123...)
    /// - By Persian name once at least 2 characters are entered.
    /// </summary>
    [HttpGet("customers/search")]
    [RequirePermission(AdminPermissions.Pos.View)]
    public async Task<ActionResult<IReadOnlyList<PosCustomerSearchItemDto>>> SearchCustomers(
        [FromQuery] string? q,
        [FromQuery] string? mode = "auto",
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var raw = (q ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Ok(Array.Empty<PosCustomerSearchItemDto>());
        }

        var take = Math.Clamp(limit, 1, 20);
        var latinDigits = NormalizeDigitsOnly(raw);
        var isPhoneSearch = string.Equals(mode, "phone", StringComparison.OrdinalIgnoreCase)
            || (string.Equals(mode, "auto", StringComparison.OrdinalIgnoreCase) && latinDigits.Length >= 5 && latinDigits.Length >= raw.Replace(" ", "").Length - 2);

        if (isPhoneSearch)
        {
            if (latinDigits.Length < 5)
            {
                return Ok(Array.Empty<PosCustomerSearchItemDto>());
            }

            // Support both "09123..." and "9123..." prefixes smoothly.
            var altPrefix = latinDigits.StartsWith("98", StringComparison.Ordinal) && latinDigits.Length >= 6
                ? "0" + latinDigits[2..]
                : latinDigits;

            var matches = await _dbContext.Customers
                .AsNoTracking()
                .Include(c => c.MembershipTier)
                .Where(c => c.IsActive &&
                    (c.PhoneNumber.StartsWith(latinDigits) ||
                     c.PhoneNumber.StartsWith(altPrefix) ||
                     c.PhoneNumber.Contains(latinDigits)))
                .OrderByDescending(c => c.PhoneNumber.StartsWith(latinDigits) || c.PhoneNumber.StartsWith(altPrefix))
                .ThenBy(c => c.PhoneNumber)
                .Take(take)
                .Select(c => new PosCustomerSearchItemDto(
                    c.Id,
                    c.PersianName,
                    c.PhoneNumber,
                    c.LoyaltyPoints,
                    c.MembershipTier != null ? c.MembershipTier.Name : null))
                .ToListAsync(cancellationToken);

            return Ok(matches);
        }

        if (raw.Length < 2)
        {
            return Ok(Array.Empty<PosCustomerSearchItemDto>());
        }

        var lowered = raw.ToLowerInvariant();
        var nameMatches = await _dbContext.Customers
            .AsNoTracking()
            .Include(c => c.MembershipTier)
            .Where(c => c.IsActive &&
                (c.PersianName.ToLower().Contains(lowered) ||
                 (latinDigits.Length >= 5 && c.PhoneNumber.StartsWith(latinDigits))))
            .OrderByDescending(c => c.PersianName.ToLower().StartsWith(lowered))
            .ThenBy(c => c.PersianName)
            .Take(take)
            .Select(c => new PosCustomerSearchItemDto(
                c.Id,
                c.PersianName,
                c.PhoneNumber,
                c.LoyaltyPoints,
                c.MembershipTier != null ? c.MembershipTier.Name : null))
            .ToListAsync(cancellationToken);

        return Ok(nameMatches);
    }

    private static string NormalizeDigitsOnly(string input)
    {
        var chars = new List<char>(input.Length);
        foreach (var ch in input)
        {
            if (ch is >= '0' and <= '9') chars.Add(ch);
            else if (ch is >= '۰' and <= '۹') chars.Add((char)('0' + (ch - '۰')));
            else if (ch is >= '٠' and <= '٩') chars.Add((char)('0' + (ch - '٠')));
        }
        return new string(chars.ToArray());
    }

    [HttpGet("orders/{id:guid}")]
    [RequirePermission(AdminPermissions.Pos.View)]
    public async Task<ActionResult<PosOrderDetailDto>> GetOrder(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new GetPosOrderQuery(id), cancellationToken));
    }

    [HttpPost("orders")]
    [RequireAnyPermission(AdminPermissions.Pos.Create, AdminPermissions.Pos.Pay, AdminPermissions.Orders.Update)]
    public async Task<ActionResult<UpsertPosOrderResultDto>> UpsertOrder(
        [FromBody] UpsertPosOrderCommand command,
        CancellationToken cancellationToken)
    {
        var adminUserId = ResolveAdminUserId();
        return Ok(await Mediator.Send(command with { ServedByAdminUserId = adminUserId }, cancellationToken));
    }

    [HttpPut("orders/{id:guid}")]
    [RequireAnyPermission(AdminPermissions.Pos.Update, AdminPermissions.Pos.Create, AdminPermissions.Pos.Pay, AdminPermissions.Orders.Update)]
    public async Task<ActionResult<UpsertPosOrderResultDto>> UpdateOrder(
        Guid id,
        [FromBody] UpsertPosOrderCommand command,
        CancellationToken cancellationToken)
    {
        var adminUserId = ResolveAdminUserId();
        return Ok(await Mediator.Send(
            command with { OrderId = id, ServedByAdminUserId = adminUserId },
            cancellationToken));
    }

    [HttpPatch("orders/{id:guid}/manager-note")]
    [RequirePermission(AdminPermissions.Pos.ManagerNote)]
    public async Task<ActionResult<object>> UpdateManagerNote(
        Guid id,
        [FromBody] UpdateManagerNoteBody body,
        CancellationToken cancellationToken)
    {
        var updated = await Mediator.Send(
            new UpdatePosOrderManagerNoteCommand(id, body.ManagerNote),
            cancellationToken);
        return Ok(new { orderId = id, managerNote = updated });
    }

    [HttpPost("orders/{id:guid}/copy")]
    [RequirePermission(AdminPermissions.Pos.Create)]
    public async Task<ActionResult<PosOrderDetailDto>> CopyOrder(Guid id, CancellationToken cancellationToken)
    {
        var adminUserId = ResolveAdminUserId();
        return Ok(await Mediator.Send(new CopyPosOrderCommand(id, adminUserId), cancellationToken));
    }

    [HttpDelete("orders/{id:guid}")]
    [RequirePermission(AdminPermissions.Pos.Delete)]
    public async Task<IActionResult> CancelOrder(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new CancelPosOrderCommand(id), cancellationToken);
        return NoContent();
    }

    private Guid? ResolveAdminUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id) ? id : null;
    }
}
