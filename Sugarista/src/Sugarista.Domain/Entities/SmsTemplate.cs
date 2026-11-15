using Sugarista.Domain.Common;

namespace Sugarista.Domain.Entities;

/// <summary>
/// A reusable message template with {Placeholder} variables. The panel can
/// also map each internal template to a provider-side Template ID / Pattern
/// (e.g. Kavenegar "Verify lookup" or SMS.ir template codes) so the same
/// content can go out through provider-native pattern APIs.
/// </summary>
public class SmsTemplate : BaseAuditableEntity
{
    public SmsTemplate(
        string name,
        string? description,
        string body,
        SmsMessageKind kind,
        bool isActive,
        bool isDefault)
    {
        Update(name, description, body, kind, isActive, isDefault);
    }

    private SmsTemplate()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public string Body { get; private set; } = string.Empty;

    public SmsMessageKind Kind { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>At most one default template per kind (enforced by handler).</summary>
    public bool IsDefault { get; private set; }

    public List<SmsTemplateProviderMapping> ProviderMappings { get; set; } = [];

    public void Update(
        string name,
        string? description,
        string body,
        SmsMessageKind kind,
        bool isActive,
        bool isDefault)
    {
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Body = body;
        Kind = kind;
        IsActive = isActive;
        IsDefault = isDefault;
    }
}
