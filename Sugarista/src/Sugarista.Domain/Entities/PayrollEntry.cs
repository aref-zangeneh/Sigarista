using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// Payroll record of one employee inside one period (فیش حقوقی):
/// auto-computed base/overtime/commission from the pay plan + work logs,
/// plus manual bonus / deductions / advances and the final payable amount.
/// </summary>
public class PayrollEntry : BaseAuditableEntity
{
    private PayrollEntry()
    {
    }

    public PayrollEntry(
        Guid periodId,
        Guid employeeId,
        string employeeName,
        string employeePosition,
        PayPlanKind payPlanKind,
        decimal workedHours,
        decimal overtimeHours,
        int shiftCount,
        decimal basePay,
        decimal overtimePay,
        decimal commissionSalesAmount,
        decimal commissionPercent)
    {
        if (periodId == Guid.Empty || employeeId == Guid.Empty)
        {
            throw new DomainException("Period and employee are required.");
        }

        if (basePay < 0)
        {
            throw new DomainException("Base pay cannot be negative.");
        }

        PeriodId = periodId;
        EmployeeId = employeeId;
        EmployeeName = employeeName;
        EmployeePosition = employeePosition;
        PayPlanKind = payPlanKind;
        WorkedHours = workedHours;
        OvertimeHours = overtimeHours;
        ShiftCount = shiftCount;
        BasePay = basePay;
        OvertimePay = overtimePay;
        CommissionSalesAmount = commissionSalesAmount;
        CommissionPercent = commissionPercent;
        CommissionPay = decimal.Round(commissionSalesAmount * commissionPercent / 100m, 0);
        Bonus = 0;
        Deductions = 0;
        Advance = 0;
        Status = PayrollEntryStatus.Draft;
    }

    public Guid PeriodId { get; private set; }

    public PayrollPeriod? Period { get; private set; }

    public Guid EmployeeId { get; private set; }

    /// <summary>Snapshots — the period's receipt must not shift if the HR file is renamed later.</summary>
    public string EmployeeName { get; private set; } = string.Empty;

    public string EmployeePosition { get; private set; } = string.Empty;

    public PayPlanKind PayPlanKind { get; private set; }

    public decimal WorkedHours { get; private set; }

    public decimal OvertimeHours { get; private set; }

    public int ShiftCount { get; private set; }

    public decimal BasePay { get; private set; }

    public decimal OvertimePay { get; private set; }

    /// <summary>فروش منسوب به این پرسنل در دوره (برای پورسانت درصدی).</summary>
    public decimal CommissionSalesAmount { get; private set; }

    public decimal CommissionPercent { get; private set; }

    public decimal CommissionPay { get; private set; }

    /// <summary>پاداش / اضافه‌پرداخت اختیاری.</summary>
    public decimal Bonus { get; private set; }

    /// <summary>کسورات (کیف‌پول، ضایعات، غیبت بدون مرخصی...).</summary>
    public decimal Deductions { get; private set; }

    /// <summary>مساعده پرداخت‌شده از قبل.</summary>
    public decimal Advance { get; private set; }

    public PayrollEntryStatus Status { get; private set; }

    public DateTimeOffset? PaidAt { get; private set; }

    public Guid? JournalEntryId { get; private set; }

    public string? Note { get; private set; }

    public decimal TotalPay => decimal
        .Round(
            BasePay + OvertimePay + CommissionPay + Bonus - Deductions - Advance,
            0,
            MidpointRounding.AwayFromZero);

    public void SetPaidInfo(decimal commissionSalesAmount, string? note)
    {
        if (Status != PayrollEntryStatus.Draft)
        {
            throw new DomainException("این فیش قبلاً پرداخت شده است.");
        }

        if (commissionSalesAmount < 0)
        {
            throw new DomainException("Sales attributed to this employee cannot be negative.");
        }

        if (Bonus < 0 || Deductions < 0 || Advance < 0)
        {
            throw new DomainException("Bonus / deductions / advances cannot be negative.");
        }

        CommissionSalesAmount = commissionSalesAmount;
        CommissionPay = decimal.Round(commissionSalesAmount * CommissionPercent / 100m, 0);
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    /// <summary>Manual adjustments on top of the auto-calculated parts (draft only).</summary>
    public void AdjustManualParts(decimal bonus, decimal deductions, decimal advance, string? note)
    {
        if (Status != PayrollEntryStatus.Draft)
        {
            throw new DomainException("فیش پرداخت‌شده قابل ویرایش نیست.");
        }

        if (bonus < 0 || deductions < 0 || advance < 0)
        {
            throw new DomainException("Bonus / deductions / advances cannot be negative.");
        }

        Bonus = bonus;
        Deductions = deductions;
        Advance = advance;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    /// <summary>Recomputes work-based parts when work logs of the period changed.</summary>
    public void RefreshWorkData(
        decimal workedHours,
        decimal overtimeHours,
        int shiftCount,
        decimal basePay,
        decimal overtimeRate,
        decimal commissionPercent)
    {
        if (Status != PayrollEntryStatus.Draft)
        {
            throw new DomainException("فیش پرداخت‌شده قابل بروزرسانی نیست.");
        }

        WorkedHours = workedHours;
        OvertimeHours = overtimeHours;
        ShiftCount = shiftCount;
        BasePay = basePay;
        OvertimePay = decimal.Round(overtimeHours * overtimeRate, 0);
        CommissionPercent = commissionPercent;
        CommissionPay = decimal.Round(CommissionSalesAmount * CommissionPercent / 100m, 0);
    }

    public void MarkPaid(DateTimeOffset now, Guid journalEntryId)
    {
        if (Status != PayrollEntryStatus.Draft)
        {
            throw new DomainException("این فیش قبلاً پرداخت شده است.");
        }

        if (TotalPay < 0)
        {
            throw new DomainException("مبلغ قابل پرداخت منفی است؛ ابتدا کسورات را اصلاح کنید.");
        }

        Status = PayrollEntryStatus.Paid;
        PaidAt = now;
        JournalEntryId = journalEntryId;
    }
}
