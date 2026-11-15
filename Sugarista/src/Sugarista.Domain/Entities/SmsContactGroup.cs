using Sugarista.Domain.Common;

namespace Sugarista.Domain.Entities;

public class SmsContactGroup : BaseAuditableEntity
{
    public SmsContactGroup(string name, string? description)
    {
        Update(name, description);
    }

    private SmsContactGroup()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    /// <summary>
    /// Smart groups have NO fixed membership: members are computed at
    /// read/send time from the stored filter (SmsSmartContactFilter JSON).
    /// </summary>
    public bool IsSmart { get; private set; }

    /// <summary>Serialized SmsSmartContactFilter — null for ordinary groups.</summary>
    public string? SmartFilterJson { get; private set; }

    public void Update(string name, string? description)
    {
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    /// <summary>Sets or clears the smart-group definition. Filter null = ordinary group.</summary>
    public void SetSmartFilter(string? smartFilterJson)
    {
        IsSmart = !string.IsNullOrWhiteSpace(smartFilterJson);
        SmartFilterJson = IsSmart ? smartFilterJson : null;
    }
}
