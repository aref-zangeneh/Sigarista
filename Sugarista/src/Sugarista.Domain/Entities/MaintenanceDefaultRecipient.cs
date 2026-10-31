using Sugarista.Domain.Common;

namespace Sugarista.Domain.Entities;

/// <summary>
/// A global fallback reminder recipient (site admin's mobile number / email).
/// Used for every schedule that has no recipient of its own, so periodic
/// service reminders never get lost just because a schedule was created
/// without an explicit target list.
/// </summary>
public class MaintenanceDefaultRecipient : BaseEntity
{
    private MaintenanceDefaultRecipient()
    {
    }

    public MaintenanceDefaultRecipient(MaintenanceChannel channel, string target, string? label)
    {
        Update(channel, target, label);
    }

    /// <summary>Where the fallback reminder is delivered: SMS or Email.</summary>
    public MaintenanceChannel Channel { get; private set; }

    /// <summary>Email address or mobile number (stored trimmed).</summary>
    public string Target { get; private set; } = string.Empty;

    /// <summary>Optional friendly name («مدیر سایت», «مدیر عملیات», ...).</summary>
    public string? Label { get; private set; }

    public void Update(MaintenanceChannel channel, string target, string? label)
    {
        Channel = MaintenanceRecipientRules.EnsureChannel(channel);
        Target = MaintenanceRecipientRules.EnsureTarget(channel, target);
        Label = MaintenanceRecipientRules.NormalizeLabel(label);
    }
}
