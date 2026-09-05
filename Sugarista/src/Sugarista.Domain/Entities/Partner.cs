using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public class Partner : BaseAuditableEntity
{
    private Partner()
    {
    }

    public Partner(string name, decimal ownershipPercentage, string? email, string? phone, string? notes)
    {
        SetName(name);
        SetOwnershipPercentage(ownershipPercentage);
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        IsActive = true;
    }

    public string Name { get; private set; } = string.Empty;

    public decimal OwnershipPercentage { get; private set; }

    public string? Email { get; private set; }

    public string? Phone { get; private set; }

    public string? Notes { get; private set; }

    public bool IsActive { get; private set; }

    public ICollection<PartnerCapitalTransaction> CapitalTransactions { get; private set; } = [];

    public void Update(string name, decimal ownershipPercentage, string? email, string? phone, string? notes, bool isActive)
    {
        SetName(name);
        SetOwnershipPercentage(ownershipPercentage);
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        IsActive = isActive;
    }

    private void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Partner name is required.");
        }

        Name = name.Trim();
    }

    private void SetOwnershipPercentage(decimal ownershipPercentage)
    {
        if (ownershipPercentage < 0 || ownershipPercentage > 100)
        {
            throw new DomainException("Ownership percentage must be between 0 and 100.");
        }

        OwnershipPercentage = ownershipPercentage;
    }
}
