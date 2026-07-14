using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public class ProductWikiGuide : BaseAuditableEntity
{
    private ProductWikiGuide()
    {
    }

    public ProductWikiGuide(Guid menuItemId)
    {
        if (menuItemId == Guid.Empty)
        {
            throw new DomainException("Menu item is required.");
        }

        MenuItemId = menuItemId;
    }

    public Guid MenuItemId { get; private set; }

    public MenuItem MenuItem { get; private set; } = null!;

    public string? Overview { get; private set; }

    public string? QualityStandards { get; private set; }

    public string? EquipmentNotes { get; private set; }

    public string? ServingNotes { get; private set; }

    public bool IsPublished { get; private set; }

    public ICollection<ProductWikiStep> Steps { get; private set; } = new List<ProductWikiStep>();

    public void Update(
        string? overview,
        string? qualityStandards,
        string? equipmentNotes,
        string? servingNotes,
        bool isPublished)
    {
        Overview = NormalizeOptionalText(overview);
        QualityStandards = NormalizeOptionalText(qualityStandards);
        EquipmentNotes = NormalizeOptionalText(equipmentNotes);
        ServingNotes = NormalizeOptionalText(servingNotes);
        IsPublished = isPublished;
    }

    private static string? NormalizeOptionalText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
