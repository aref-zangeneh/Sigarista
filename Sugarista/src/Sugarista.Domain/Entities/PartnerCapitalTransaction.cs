using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public class PartnerCapitalTransaction : BaseAuditableEntity
{
    private PartnerCapitalTransaction()
    {
    }

    public PartnerCapitalTransaction(
        Guid partnerId,
        PartnerCapitalTransactionType type,
        decimal amount,
        DateOnly transactionDate,
        string? description,
        Guid? journalEntryId = null)
    {
        if (partnerId == Guid.Empty)
        {
            throw new DomainException("Partner is required.");
        }

        if (amount <= 0)
        {
            throw new DomainException("Amount must be greater than zero.");
        }

        PartnerId = partnerId;
        Type = type;
        Amount = amount;
        TransactionDate = transactionDate;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        JournalEntryId = journalEntryId;
    }

    public Guid PartnerId { get; private set; }

    public Partner Partner { get; private set; } = null!;

    public PartnerCapitalTransactionType Type { get; private set; }

    public decimal Amount { get; private set; }

    public DateOnly TransactionDate { get; private set; }

    public string? Description { get; private set; }

    public Guid? JournalEntryId { get; private set; }

    public JournalEntry? JournalEntry { get; private set; }

    public void LinkJournalEntry(Guid journalEntryId)
    {
        JournalEntryId = journalEntryId;
    }
}
