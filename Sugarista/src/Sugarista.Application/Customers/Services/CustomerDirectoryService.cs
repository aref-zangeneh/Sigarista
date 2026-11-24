using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Common.Text;
using Sugarista.Application.Sms;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Customers.Services;

/// <summary>
/// Unified customer directory: every order channel (POS, online checkout, OTP login, admin edits)
/// resolves the customer by canonical phone — creating/updating the Customer profile AND
/// syncing the phone into SmsContacts (address book) automatically without duplicates.
/// </summary>
public static class CustomerDirectoryService
{
    public static string NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return string.Empty;
        }

        var english = DigitNormalizer.ToEnglishDigits(phone) ?? string.Empty;
        var cleaned = english
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty)
            .Replace("(", string.Empty)
            .Replace(")", string.Empty)
            .Trim();

        if (string.IsNullOrWhiteSpace(cleaned) ||
            string.Equals(cleaned, "POS", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var smsMobile = SmsTextUtilities.NormalizeMobile(cleaned);
        if (smsMobile is not null)
        {
            return SmsTextUtilities.ToDisplayMobile(smsMobile);
        }

        return cleaned;
    }

    public static bool IsMeaningfulCustomerName(string? name, string? phone = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var trimmed = name.Trim();
        if (trimmed is "مهمان" or "مهمان گرامی" or "POS")
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(phone) &&
            string.Equals(NormalizePhone(trimmed), NormalizePhone(phone), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var ascii = DigitNormalizer.ToEnglishDigits(trimmed) ?? trimmed;
        return !ascii.All(ch => char.IsAsciiDigit(ch) || ch is '+' or '-' or ' ');
    }

    public static (string? FirstName, string? LastName) SplitPersonName(string? fullName, string? phone = null)
    {
        if (!IsMeaningfulCustomerName(fullName, phone))
        {
            return (null, null);
        }

        var tokens = fullName!
            .Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (tokens.Length == 0)
        {
            return (null, null);
        }

        if (tokens.Length == 1)
        {
            var single = tokens[0].Length > 80 ? tokens[0][..80] : tokens[0];
            return (single, null);
        }

        var first = tokens[0].Length > 80 ? tokens[0][..80] : tokens[0];
        var rest = string.Join(' ', tokens.Skip(1));
        var last = rest.Length > 80 ? rest[..80] : rest;
        return (first, last);
    }

    public static async Task<Customer?> ResolveCustomerAsync(
        IApplicationDbContext dbContext,
        string? customerName,
        string? customerPhone,
        string channel,
        CancellationToken cancellationToken)
    {
        var phone = NormalizePhone(customerPhone);
        var name = customerName?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(phone) || phone.Length < 4)
        {
            return null;
        }

        var smsMobile = SmsTextUtilities.NormalizeMobile(phone);
        var altPlusMobile = smsMobile is not null ? $"+{smsMobile}" : null;

        var customer = dbContext.Customers.Local.FirstOrDefault(entry =>
            entry.PhoneNumber == phone ||
            (smsMobile != null && (entry.PhoneNumber == smsMobile || entry.PhoneNumber == altPlusMobile)));

        customer ??= await dbContext.Customers.FirstOrDefaultAsync(
            entry => entry.PhoneNumber == phone ||
                     (smsMobile != null && (entry.PhoneNumber == smsMobile || entry.PhoneNumber == altPlusMobile)),
            cancellationToken);

        var hasMeaningfulName = IsMeaningfulCustomerName(name, phone);

        if (customer is null)
        {
            customer = new Customer(
                hasMeaningfulName ? name : phone,
                phone,
                channel);
            dbContext.Customers.Add(customer);
        }
        else if (hasMeaningfulName && name != customer.PersianName)
        {
            customer.Update(name, customer.Email, customer.Notes, customer.IsActive);
        }

        customer.MarkSeen(channel);

        if (smsMobile is not null)
        {
            await EnsureSmsContactSyncedAsync(
                dbContext,
                smsMobile,
                hasMeaningfulName ? name : customer.PersianName,
                phone,
                customer.Email,
                channel,
                cancellationToken);
        }

        return customer;
    }

    private static async Task EnsureSmsContactSyncedAsync(
        IApplicationDbContext dbContext,
        string normalizedMobile,
        string? rawName,
        string displayPhone,
        string? email,
        string channel,
        CancellationToken cancellationToken)
    {
        var contact = dbContext.SmsContacts.Local.FirstOrDefault(c => c.Mobile == normalizedMobile)
            ?? await dbContext.SmsContacts.FirstOrDefaultAsync(c => c.Mobile == normalizedMobile, cancellationToken);

        var (firstName, lastName) = SplitPersonName(rawName, displayPhone);

        if (contact is null)
        {
            var noteLabel = string.Equals(channel, "pos", StringComparison.OrdinalIgnoreCase)
                ? "ثبت خودکار از صندوق فروش (POS)"
                : $"ثبت خودکار ({channel})";

            dbContext.SmsContacts.Add(new SmsContact(
                firstName,
                lastName,
                normalizedMobile,
                email,
                noteLabel,
                isActive: true,
                marketingConsent: true));
            return;
        }

        if (string.IsNullOrWhiteSpace(contact.FirstName) &&
            string.IsNullOrWhiteSpace(contact.LastName) &&
            (firstName is not null || lastName is not null))
        {
            contact.Update(
                firstName,
                lastName,
                normalizedMobile,
                contact.Email ?? email,
                contact.Notes,
                contact.IsActive,
                contact.MarketingConsent);
        }
    }
}
