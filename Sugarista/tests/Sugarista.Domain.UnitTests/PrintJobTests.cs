using Sugarista.Domain.Entities;

namespace Sugarista.Domain.UnitTests;

public class PrintJobTests
{
    [Fact]
    public void Create_ShouldStartAsPending()
    {
        var job = PrintJob.Create(
            Guid.NewGuid(),
            "SG-TEST01",
            PrintJobCopyType.Customer,
            "{}");

        Assert.Equal(PrintJobStatus.Pending, job.Status);
        Assert.Equal(PrintJobCopyType.Customer, job.CopyType);
        Assert.Equal(0, job.AttemptCount);
        Assert.Null(job.PrintedAt);
        Assert.Null(job.DispatchedAt);
    }

    [Fact]
    public void MarkDispatched_ThenPrinted_ShouldTrackTimestamps()
    {
        var job = PrintJob.Create(Guid.NewGuid(), "SG-TEST01", PrintJobCopyType.Barista, "{}");

        var dispatchedAt = new DateTimeOffset(2026, 11, 2, 20, 0, 0, TimeSpan.Zero);
        job.MarkDispatched(dispatchedAt);
        Assert.Equal(PrintJobStatus.Dispatched, job.Status);
        Assert.Equal(dispatchedAt, job.DispatchedAt);

        var printedAt = new DateTimeOffset(2026, 11, 2, 20, 0, 5, TimeSpan.Zero);
        job.MarkPrinted(printedAt);
        Assert.Equal(PrintJobStatus.Printed, job.Status);
        Assert.Equal(printedAt, job.PrintedAt);
    }

    [Fact]
    public void MarkFailed_ShouldCountAttemptAndKeepLastError()
    {
        var job = PrintJob.Create(Guid.NewGuid(), "SG-TEST01", PrintJobCopyType.Customer, "{}");

        job.MarkFailed("printer offline");

        Assert.Equal(PrintJobStatus.Failed, job.Status);
        Assert.Equal(1, job.AttemptCount);
        Assert.Equal("printer offline", job.LastError);
    }

    [Fact]
    public void IsStaleDispatch_ShouldOnlyBeTrueForOldDispatches()
    {
        var job = PrintJob.Create(Guid.NewGuid(), "SG-TEST01", PrintJobCopyType.Customer, "{}");
        var retention = TimeSpan.FromMinutes(2);

        Assert.False(job.IsStaleDispatch(retention));

        job.MarkDispatched(DateTimeOffset.UtcNow - TimeSpan.FromMinutes(5));
        Assert.True(job.IsStaleDispatch(retention));

        job.MarkDispatched(DateTimeOffset.UtcNow - TimeSpan.FromSeconds(10));
        Assert.False(job.IsStaleDispatch(retention));

        job.MarkPrinted();
        Assert.False(job.IsStaleDispatch(retention));
    }

    [Fact]
    public void MarkBackToPending_ShouldClearDispatch()
    {
        var job = PrintJob.Create(Guid.NewGuid(), "SG-TEST01", PrintJobCopyType.Customer, "{}");
        job.MarkDispatched();

        job.MarkBackToPending();

        Assert.Equal(PrintJobStatus.Pending, job.Status);
        Assert.Null(job.DispatchedAt);
    }
}
