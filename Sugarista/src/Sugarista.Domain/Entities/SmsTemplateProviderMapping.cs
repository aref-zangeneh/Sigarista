using Sugarista.Domain.Common;

namespace Sugarista.Domain.Entities;

/// <summary>
/// Links an internal template to a provider-side template/pattern identifier,
/// because providers manage their own approved templates (Kavenegar Verify,
/// SMS.ir Template) with their own IDs.
/// </summary>
public class SmsTemplateProviderMapping : BaseEntity
{
    public SmsTemplateProviderMapping(Guid templateId, SmsProviderType providerType, string providerTemplateId)
    {
        TemplateId = templateId;
        ProviderType = providerType;
        Update(providerTemplateId);
    }

    private SmsTemplateProviderMapping()
    {
    }

    public Guid TemplateId { get; private set; }

    public SmsProviderType ProviderType { get; private set; }

    /// <summary>The provider's own template/pattern identifier.</summary>
    public string ProviderTemplateId { get; private set; } = string.Empty;

    public void Update(string providerTemplateId)
    {
        ProviderTemplateId = providerTemplateId.Trim();
    }
}
