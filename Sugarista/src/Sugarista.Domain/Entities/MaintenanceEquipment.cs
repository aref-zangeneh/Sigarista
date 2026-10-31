using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// One maintenance equipment unit (تجهیزاتی با سرویس دوره‌ای): espresso machine,
/// grinder, HVAC, ... along with its identity details, recurring service
/// schedules and修理 history. Stays in the ledger forever — «از رده خارج» is a
/// state, not a deletion.
/// </summary>
public class MaintenanceEquipment : BaseAuditableEntity
{
    private MaintenanceEquipment()
    {
    }

    public MaintenanceEquipment(
        string persianName,
        string? category,
        string? brandModel,
        string? serialNumber,
        string? location,
        DateOnly? purchaseDate,
        string? notes)
    {
        SetName(persianName);

        Category = NullIfEmpty(category);
        BrandModel = NullIfEmpty(brandModel);
        SerialNumber = NullIfEmpty(serialNumber);
        Location = NullIfEmpty(location);
        Notes = NullIfEmpty(notes);
        PurchaseDate = purchaseDate;
        IsActive = true;
        Schedules = [];
        ServiceLogs = [];
    }

    public string PersianName { get; private set; } = string.Empty;

    public string? Category { get; private set; }

    public string? BrandModel { get; private set; }

    public string? SerialNumber { get; private set; }

    /// <summary>Where the unit physically lives (بار، آشپزخانه، سالن، ...).</summary>
    public string? Location { get; private set; }

    public DateOnly? PurchaseDate { get; private set; }

    public string? Notes { get; private set; }

    /// <summary>false = از رده خارج (خراب/جایگزین‌شده) و دیگر یادآوری نمی‌شود.</summary>
    public bool IsActive { get; private set; }

    public string? OutOfServiceNote { get; private set; }

    public DateTimeOffset? OutOfServiceAt { get; private set; }

    public ICollection<MaintenanceSchedule> Schedules { get; private set; } = [];

    public ICollection<MaintenanceServiceLog> ServiceLogs { get; private set; } = [];

    public void Update(
        string persianName,
        string? category,
        string? brandModel,
        string? serialNumber,
        string? location,
        DateOnly? purchaseDate,
        string? notes)
    {
        SetName(persianName);

        Category = NullIfEmpty(category);
        BrandModel = NullIfEmpty(brandModel);
        SerialNumber = NullIfEmpty(serialNumber);
        Location = NullIfEmpty(location);
        Notes = NullIfEmpty(notes);
        PurchaseDate = purchaseDate;
    }

    public void TakeOutOfService(string? note)
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        OutOfServiceNote = NullIfEmpty(note);
        OutOfServiceAt = DateTimeOffset.UtcNow;
    }

    public void BringBackToService()
    {
        if (IsActive)
        {
            return;
        }

        IsActive = true;
        OutOfServiceNote = null;
        OutOfServiceAt = null;
    }

    private static string? NullIfEmpty(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private void SetName(string persianName)
    {
        if (string.IsNullOrWhiteSpace(persianName))
        {
            throw new DomainException("نام تجهیز الزامی است.");
        }

        PersianName = persianName.Trim();
    }
}
