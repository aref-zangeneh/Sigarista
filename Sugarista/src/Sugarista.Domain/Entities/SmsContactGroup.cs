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

    public void Update(string name, string? description)
    {
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }
}
