using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// Admin-defined override for one tooltip/help bubble on the admin panel.
/// <see cref="Key"/> matches a key in the frontend tooltip registry
/// (e.g. <c>pos.pay_cash</c>); when no row exists the registry default text
/// is shown. Deactivating a row hides that tooltip everywhere.
/// </summary>
public class AdminTooltip : BaseAuditableEntity
{
    public AdminTooltip(
        string key,
        string text,
        bool isActive = true)
    {
        Update(key, text, isActive);
    }

    private AdminTooltip()
    {
    }

    /// <summary>Stable registry key, e.g. <c>pos.pay_cash</c>.</summary>
    public string Key { get; private set; } = string.Empty;

    /// <summary>Persian help text shown in the tooltip bubble.</summary>
    public string Text { get; private set; } = string.Empty;

    /// <summary>Disabled tooltips are hidden from every admin page.</summary>
    public bool IsActive { get; private set; }

    public void Update(
        string key,
        string text,
        bool isActive)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new DomainException("Tooltip key is required.");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new DomainException("Tooltip text is required.");
        }

        Key = key.Trim();
        Text = text.Trim();
        IsActive = isActive;
    }
}
