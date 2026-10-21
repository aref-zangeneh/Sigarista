using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// One payroll month (دوره حقوق و دستمزد). Entries are generated from
/// work logs + the current pay plan of every active (or in-period active) employee.
/// </summary>
public class PayrollPeriod : BaseAuditableEntity
{
    private PayrollPeriod()
    {
    }

    public PayrollPeriod(int shamsiYear, int shamsiMonth, DateOnly startDate, DateOnly endDate)
    {
        if (shamsiYear is < 1300 or > 1500)
        {
            throw new DomainException("Shamsi year is invalid.");
        }

        if (shamsiMonth is < 1 or > 12)
        {
            throw new DomainException("Shamsi month must be between 1 and 12.");
        }

        if (endDate < startDate)
        {
            throw new DomainException("Period end date must be after the start date.");
        }

        ShamsiYear = shamsiYear;
        ShamsiMonth = shamsiMonth;
        StartDate = startDate;
        EndDate = endDate;
        Status = PayrollPeriodStatus.Draft;
        Entries = [];
    }

    public int ShamsiYear { get; private set; }

    public int ShamsiMonth { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly EndDate { get; private set; }

    public PayrollPeriodStatus Status { get; private set; }

    public string? Note { get; private set; }

    public ICollection<PayrollEntry> Entries { get; private set; } = [];

    public string MonthLabel => $"{ShamsiMonth:00}/{ShamsiYear:0000}";

    public void FinalizePeriod()
    {
        if (Entries.Any(entry => entry.Status == PayrollEntryStatus.Paid))
        {
            // paid entries keep their state; period still becomes final for the rest
        }

        Status = PayrollPeriodStatus.Finalized;
    }

    /// <summary>Back to draft only when nothing is paid yet.</summary>
    public void Reopen()
    {
        if (Entries.Any(entry => entry.Status == PayrollEntryStatus.Paid))
        {
            throw new DomainException("دوره‌ای که پرداخت‌شده دارد قابل بازگشایی نیست.");
        }

        Status = PayrollPeriodStatus.Draft;
    }

    public void EnsureEditable()
    {
        if (Status != PayrollPeriodStatus.Draft)
        {
            throw new DomainException("این دوره قطعی شده است و قابل ویرایش نیست.");
        }
    }

    public void EnsureFinalizedForPayment()
    {
        if (Status == PayrollPeriodStatus.Draft)
        {
            throw new DomainException("برای پرداخت، ابتدا دوره را قطعی کنید.");
        }
    }

    public void MarkFullyPaidIfSettled()
    {
        // a zero/negative receipt has nothing owed — it counts as settled
        if (Entries.Count > 0 && Entries.All(entry =>
                entry.Status == PayrollEntryStatus.Paid || entry.TotalPay <= 0))
        {
            Status = PayrollPeriodStatus.FullyPaid;
        }
    }
}
