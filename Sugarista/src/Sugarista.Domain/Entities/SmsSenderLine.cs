using Sugarista.Domain.Common;

namespace Sugarista.Domain.Entities;

/// <summary>An outbound sender number (line) registered on a provider.</summary>
public class SmsSenderLine : BaseEntity
{
    public SmsSenderLine(Guid providerId, string number, string? label, bool isDefault)
    {
        ProviderId = providerId;
        Update(number, label, isDefault);
    }

    private SmsSenderLine()
    {
    }

    public Guid ProviderId { get; private set; }

    public string Number { get; private set; } = string.Empty;

    public string? Label { get; private set; }

    public bool IsDefaultLine { get; private set; }

    public void Update(string number, string? label, bool isDefault)
    {
        Number = number.Trim();
        Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim();
        IsDefaultLine = isDefault;
    }
}
