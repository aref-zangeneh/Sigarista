using Sugarista.Domain.Common;

namespace Sugarista.Domain.Entities;

/// <summary>
/// Editable Persian definition of an HR term — the content of the «؟»
/// tooltips. Merchants can rewrite every definition to match their cafe's
/// own payroll language.
/// </summary>
public class HrGlossaryTerm : BaseAuditableEntity
{
    private HrGlossaryTerm()
    {
    }

    public HrGlossaryTerm(string key, string definition)
    {
        Key = key.Trim();
        Definition = definition.Trim();
    }

    public string Key { get; private set; } = string.Empty;

    public string Definition { get; set; } = string.Empty;
}
