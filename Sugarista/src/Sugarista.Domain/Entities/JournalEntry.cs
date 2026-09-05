using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public class JournalEntry : BaseAuditableEntity
{
    private JournalEntry()
    {
    }

    public JournalEntry(
        string referenceNumber,
        DateOnly entryDate,
        JournalSourceType sourceType,
        string description,
        Guid? sourceId = null)
    {
        if (string.IsNullOrWhiteSpace(referenceNumber))
        {
            throw new DomainException("Reference number is required.");
        }

        ReferenceNumber = referenceNumber.Trim();
        EntryDate = entryDate;
        SourceType = sourceType;
        SourceId = sourceId;
        Description = string.IsNullOrWhiteSpace(description) ? string.Empty : description.Trim();
        IsPosted = true;
        PostedAt = DateTimeOffset.UtcNow;
    }

    public string ReferenceNumber { get; private set; } = string.Empty;

    public DateOnly EntryDate { get; private set; }

    public JournalSourceType SourceType { get; private set; }

    public Guid? SourceId { get; private set; }

    public string Description { get; private set; } = string.Empty;

    public bool IsPosted { get; private set; }

    public DateTimeOffset? PostedAt { get; private set; }

    public ICollection<JournalLine> Lines { get; private set; } = [];

    public void AddLine(JournalLine line)
    {
        if (!IsPosted)
        {
            throw new DomainException("Cannot add lines to an unposted entry.");
        }

        Lines.Add(line);
    }

    public void ValidateBalanced()
    {
        if (Lines.Count < 2)
        {
            throw new DomainException("A journal entry must contain at least two lines.");
        }

        var totalDebit = Lines.Sum(line => line.Debit);
        var totalCredit = Lines.Sum(line => line.Credit);

        if (totalDebit != totalCredit)
        {
            throw new DomainException($"Journal entry is not balanced. Debit={totalDebit}, Credit={totalCredit}.");
        }

        if (totalDebit <= 0)
        {
            throw new DomainException("Journal entry amount must be greater than zero.");
        }
    }
}
