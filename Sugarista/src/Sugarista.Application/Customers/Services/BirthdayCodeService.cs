using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Customers.Services;

/// <summary>
/// Code-mode birthday benefit: each customer who is inside their birthday
/// window gets a personal, single-use discount code issued through the
/// regular discount engine — so checkout needs no special handling at all.
/// The row carries a stable description marker so at most one code per
/// customer per year is ever created.
/// </summary>
public static class BirthdayCodeService
{
    public static string BuildMarker(Guid customerId, DateOnly birthday) =>
        $"birthday:{customerId:N}:{birthday.Day:d2}/{birthday.Month:d2}";

    public static async Task<Discount?> EnsurePersonalCodeAsync(
        IApplicationDbContext dbContext,
        Customer customer,
        Sugarista.Domain.Entities.SiteSettings settings,
        CancellationToken cancellationToken = default)
    {
        if (settings is not { BirthdayDiscountEnabled: true } ||
            settings.BirthdayDiscountMode != BirthdayDiscountMode.Code ||
            settings.BirthdayDiscountPercent <= 0 ||
            customer.DateOfBirth is null)
        {
            return null;
        }

        var (startsAt, endsAt) = BirthdayDiscountService.GetActiveWindow(
            customer.DateOfBirth.Value, settings.BirthdayDiscountWindowDays, DateTimeOffset.UtcNow);
        if (startsAt is null || endsAt is null)
        {
            return null;
        }

        var marker = BuildMarker(customer.Id, customer.DateOfBirth.Value);

        var existing = await dbContext.Discounts
            .AsNoTracking()
            .Where(discount => discount.Description != null && discount.Description == marker)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var code = "TOL-" + RandomNumberGenerator.GetHexString(6).ToUpperInvariant();

        var discount = new Discount(
            name: $"تولد {customer.PersianName}",
            description: marker,
            code: code,
            type: DiscountType.AmountOff,
            valueKind: DiscountValueKind.Percentage,
            value: settings.BirthdayDiscountPercent,
            maxDiscountAmount: null,
            minCartAmount: 0m,
            startsAt: startsAt,
            endsAt: endsAt,
            isActive: true,
            priority: 100,
            isStackable: true,
            maxTotalUses: 1,
            maxUsesPerCustomer: 1,
            appliesTo: DiscountAppliesTo.Order,
            customerGroup: DiscountCustomerGroup.All);
        dbContext.Discounts.Add(discount);
        await dbContext.SaveChangesAsync(cancellationToken);
        return discount;
    }
}
