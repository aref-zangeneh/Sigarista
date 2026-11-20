using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Accounting.Services;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.UnitTests.Accounting;

public class JournalReferenceNumberTests
{
    [Fact]
    public void ComputeNextReferenceNumber_EmptyDay_ShouldStartAt0001()
    {
        var date = new DateOnly(2026, 11, 20);

        var next = AccountingService.ComputeNextReferenceNumber(date, []);

        Assert.Equal("JE-20261120-0001", next);
    }

    [Fact]
    public void ComputeNextReferenceNumber_WithGapsAfterDeletion_ShouldAdvancePastMaxInsteadOfCount()
    {
        var date = new DateOnly(2026, 11, 20);
        // Entry 0002 was deleted/excluded: count is 2, max is 3 — must NOT collide with 0003!
        var existing = new[] { "JE-20261120-0001", "JE-20261120-0003" };

        var next = AccountingService.ComputeNextReferenceNumber(date, existing);

        Assert.Equal("JE-20261120-0004", next);
    }

    [Fact]
    public void ComputeNextReferenceNumber_ShouldRespectHighWaterMarkForConcurrentRequests()
    {
        var date = new DateOnly(2026, 11, 20);
        var existing = new[] { "JE-20261120-0001", "JE-20261120-0002" };

        // Another concurrent request already reserved 0003 before SaveChangesAsync committed
        var next = AccountingService.ComputeNextReferenceNumber(date, existing, minSequenceExclusive: 3);

        Assert.Equal("JE-20261120-0004", next);
    }

    [Fact]
    public void ComputeNextReferenceNumber_ShouldIgnoreOtherDatesAndNonNumericSuffixes()
    {
        var date = new DateOnly(2026, 11, 20);
        var existing = new[]
        {
            "JE-20261119-0099",
            "JE-20261120-MANUAL",
            "JE-20261120-0007",
        };

        var next = AccountingService.ComputeNextReferenceNumber(date, existing);

        Assert.Equal("JE-20261120-0008", next);
    }

    [Fact]
    public void ComputeNextReferenceNumber_Above9999_ShouldFormatCleanlyWithoutTruncation()
    {
        var date = new DateOnly(2026, 11, 20);
        var existing = new[] { "JE-20261120-9999" };

        var next = AccountingService.ComputeNextReferenceNumber(date, existing);

        Assert.Equal("JE-20261120-10000", next);
    }

    [Fact]
    public void IsDuplicateReferenceException_ShouldDetectPostgresReferenceUniqueViolation()
    {
        var inner = new InvalidOperationException("23505: duplicate key value violates unique constraint \"IX_JournalEntries_ReferenceNumber\"");
        var ex = new DbUpdateException("Save failed", inner);

        Assert.True(AccountingService.IsDuplicateReferenceException(ex));

        var other = new DbUpdateException("Save failed", new InvalidOperationException("connection timeout"));
        Assert.False(AccountingService.IsDuplicateReferenceException(other));
    }

    [Fact]
    public void JournalEntry_UpdateReferenceNumber_ShouldReplaceReferenceCleanly()
    {
        var entry = new JournalEntry("JE-20261120-0001", new DateOnly(2026, 11, 20), JournalSourceType.OrderPayment, "Test");
        entry.UpdateReferenceNumber("  JE-20261120-0005  ");

        Assert.Equal("JE-20261120-0005", entry.ReferenceNumber);
    }
}
