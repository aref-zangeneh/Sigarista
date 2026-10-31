using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>Where a service reminder is delivered: SMS or Email.</summary>
public enum MaintenanceChannel
{
    Sms = 1,
    Email = 2,
}

/// <summary>
/// One dynamic reminder recipient (a mobile number or an email address) of a
/// maintenance schedule. Recipients are freely edited — any count, any mix.
/// </summary>
public class MaintenanceRecipient : BaseEntity
{
    private MaintenanceRecipient()
    {
    }

    public MaintenanceRecipient(
        Guid scheduleId,
        MaintenanceChannel channel,
        string target,
        string? label)
    {
        if (scheduleId == Guid.Empty)
        {
            throw new DomainException("زمان‌بندی سرویس نامعتبر است.");
        }

        ScheduleId = scheduleId;
        Channel = MaintenanceRecipientRules.EnsureChannel(channel);
        Target = MaintenanceRecipientRules.EnsureTarget(channel, target);
        Label = MaintenanceRecipientRules.NormalizeLabel(label);
    }

    public Guid ScheduleId { get; private set; }

    public MaintenanceSchedule Schedule { get; private set; } = null!;

    public MaintenanceChannel Channel { get; private set; }

    /// <summary>Email address or mobile number (stored trimmed).</summary>
    public string Target { get; private set; } = string.Empty;

    /// <summary>Optional friendly name («مدیر عملیات», «مدیر انبار», ...).</summary>
    public string? Label { get; private set; }

    public void Update(MaintenanceChannel channel, string target, string? label)
    {
        Channel = MaintenanceRecipientRules.EnsureChannel(channel);
        Target = MaintenanceRecipientRules.EnsureTarget(channel, target);
        Label = MaintenanceRecipientRules.NormalizeLabel(label);
    }
}

/// <summary>
/// Validation shared by per-schedule recipients and the global fallback list,
/// so an email/mobile number is accepted or rejected the same way everywhere.
/// </summary>
internal static class MaintenanceRecipientRules
{
    private const string EmailChars = "@";

    public static MaintenanceChannel EnsureChannel(MaintenanceChannel channel)
    {
        if (channel is not (MaintenanceChannel.Sms or MaintenanceChannel.Email))
        {
            throw new DomainException("کانال یادآوری نامعتبر است.");
        }

        return channel;
    }

    public static string EnsureTarget(MaintenanceChannel channel, string target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            throw channel == MaintenanceChannel.Email
                ? new DomainException("ایمیل یادآوری الزامی است.")
                : new DomainException("شماره موبایل یادآوری الزامی است.");
        }

        var trimmed = target.Trim();

        if (channel == MaintenanceChannel.Email && !trimmed.Contains(EmailChars, StringComparison.Ordinal))
        {
            throw new DomainException("قالب ایمیل یادآوری صحیح نیست.");
        }

        if (channel == MaintenanceChannel.Sms)
        {
            var digits = new string(trimmed.Where(char.IsDigit).ToArray());
            if (digits.Length < 8)
            {
                throw new DomainException("شماره موبایل یادآوری صحیح نیست.");
            }

            trimmed = digits;
        }

        return trimmed;
    }

    public static string? NormalizeLabel(string? label)
    {
        return string.IsNullOrWhiteSpace(label) ? null : label.Trim();
    }
}
