using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public class JournalLine : BaseEntity
{
    private JournalLine()
    {
    }

    public JournalLine(
        Guid journalEntryId,
        Guid accountId,
        decimal debit,
        decimal credit,
        string? description = null,
        Guid? partnerId = null)
    {
        if (journalEntryId == Guid.Empty)
        {
            throw new DomainException("Journal entry is required.");
        }

        if (accountId == Guid.Empty)
        {
            throw new DomainException("Account is required.");
        }

        if (debit < 0 || credit < 0)
        {
            throw new DomainException("Debit and credit must be non-negative.");
        }

        if (debit > 0 && credit > 0)
        {
            throw new DomainException("A journal line cannot have both debit and credit.");
        }

        if (debit == 0 && credit == 0)
        {
            throw new DomainException("A journal line must have a debit or credit amount.");
        }

        JournalEntryId = journalEntryId;
        AccountId = accountId;
        Debit = debit;
        Credit = credit;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        PartnerId = partnerId;
    }

    public Guid JournalEntryId { get; private set; }

    public JournalEntry JournalEntry { get; private set; } = null!;

    public Guid AccountId { get; private set; }

    public Account Account { get; private set; } = null!;

    public decimal Debit { get; private set; }

    public decimal Credit { get; private set; }

    public string? Description { get; private set; }

    public Guid? PartnerId { get; private set; }

    public Partner? Partner { get; private set; }
}
