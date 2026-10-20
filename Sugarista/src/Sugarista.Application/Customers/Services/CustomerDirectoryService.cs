using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Customers.Services;

/// <summary>
/// Unified customer directory: every order channel (POS, online checkout, admin edits)
/// resolves the customer by phone — creating the profile once and reusing it afterwards.
/// </summary>
internal static class CustomerDirectoryService
{
    internal static string NormalizePhone(string? phone) => phone?.Replace(" ", string.Empty).Trim() ?? string.Empty;

    internal static async Task<Customer?> ResolveCustomerAsync(
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

        var customer = await dbContext.Customers
            .FirstOrDefaultAsync(entry => entry.PhoneNumber == phone, cancellationToken);

        if (customer is null)
        {
            customer = new Customer(
                string.IsNullOrWhiteSpace(name) ? phone : name,
                phone,
                channel);
            dbContext.Customers.Add(customer);
        }
        else if (!string.IsNullOrWhiteSpace(name) && name != customer.PersianName && name != "مهمان")
        {
            customer.Update(name, customer.Email, customer.Notes, customer.IsActive);
        }

        customer.MarkSeen(channel);
        return customer;
    }
}
