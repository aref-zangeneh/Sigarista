using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public class Supplier : BaseAuditableEntity
{
    public Supplier()
    {
    }

    public Supplier(
        string persianName,
        string phoneNumber,
        string contactName,
        string notes,
        int displayOrder,
        bool isActive = true,
        IReadOnlyCollection<string>? phoneNumbers = null)
    {
        Update(persianName, phoneNumber, contactName, notes, displayOrder, isActive, phoneNumbers);
    }

    public string PersianName { get; private set; } = string.Empty;

    /// <summary>Primary phone — kept as the first phone of <see cref="PhoneNumbers"/> for existing readers.</summary>
    public string PhoneNumber { get; private set; } = string.Empty;

    /// <summary>All phone numbers (first one mirrors <see cref="PhoneNumber"/>).</summary>
    public string[] PhoneNumbers { get; private set; } = [];

    public string ContactName { get; private set; } = string.Empty;

    public string Notes { get; private set; } = string.Empty;

    public int DisplayOrder { get; private set; }

    public bool IsActive { get; private set; } = true;

    public ICollection<IngredientSupplier> IngredientSuppliers { get; private set; } = [];

    public void Update(
        string persianName,
        string phoneNumber,
        string contactName,
        string notes,
        int displayOrder,
        bool isActive,
        IReadOnlyCollection<string>? phoneNumbers = null)
    {
        if (string.IsNullOrWhiteSpace(persianName))
        {
            throw new DomainException("Supplier name is required.");
        }

        if (persianName.Trim().Length > 200)
        {
            throw new DomainException("Supplier name is too long.");
        }

        PersianName = persianName.Trim();
        ContactName = (contactName ?? string.Empty).Trim();
        Notes = (notes ?? string.Empty).Trim();
        DisplayOrder = displayOrder < 0 ? 0 : displayOrder;
        IsActive = isActive;

        ApplyPhoneNumbers(phoneNumber, phoneNumbers);
    }

    private void ApplyPhoneNumbers(string phoneNumber, IReadOnlyCollection<string>? phoneNumbers)
    {
        // Merge the legacy single field with the multi-phone list, then
        // normalize: trim, drop empties, deduplicate, cap at 10 entries of 40 chars.
        var candidates = new List<string>();
        if (phoneNumbers is not null)
        {
            candidates.AddRange(phoneNumbers);
        }
        else if (!string.IsNullOrWhiteSpace(phoneNumber))
        {
            // Legacy callers without the list keep their single phone.
            candidates.Add(phoneNumber);
        }

        var normalized = new List<string>();
        foreach (var candidate in candidates)
        {
            var phone = (candidate ?? string.Empty).Trim();
            if (phone.Length == 0 || phone.Length > 40)
            {
                continue;
            }

            if (normalized.Contains(phone, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            normalized.Add(phone);
            if (normalized.Count == MaxPhoneNumbers)
            {
                break;
            }
        }

        if (phoneNumbers is not null && normalized.Count == 0)
        {
            // An explicit (possibly empty) list that normalizes to nothing means "no phones".
            normalized = [];
        }

        PhoneNumbers = [.. normalized];
        PhoneNumber = normalized.Count > 0 ? normalized[0] : string.Empty;
    }

    private const int MaxPhoneNumbers = 10;
}
