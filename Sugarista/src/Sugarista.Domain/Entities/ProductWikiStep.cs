using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public class ProductWikiStep : BaseAuditableEntity
{
    private ProductWikiStep()
    {
    }

    public ProductWikiStep(
        Guid menuItemId,
        int stepOrder,
        string title,
        string instructions,
        int? durationMinutes,
        bool isQualityCheckpoint)
    {
        Update(menuItemId, stepOrder, title, instructions, durationMinutes, isQualityCheckpoint);
    }

    public Guid MenuItemId { get; private set; }

    public MenuItem MenuItem { get; private set; } = null!;

    public int StepOrder { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Instructions { get; private set; } = string.Empty;

    public int? DurationMinutes { get; private set; }

    public bool IsQualityCheckpoint { get; private set; }

    public void Update(
        Guid menuItemId,
        int stepOrder,
        string title,
        string instructions,
        int? durationMinutes,
        bool isQualityCheckpoint)
    {
        if (menuItemId == Guid.Empty)
        {
            throw new DomainException("Menu item is required.");
        }

        if (stepOrder <= 0)
        {
            throw new DomainException("Step order must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("Step title is required.");
        }

        if (string.IsNullOrWhiteSpace(instructions))
        {
            throw new DomainException("Step instructions are required.");
        }

        if (durationMinutes is <= 0)
        {
            throw new DomainException("Duration must be greater than zero when provided.");
        }

        MenuItemId = menuItemId;
        StepOrder = stepOrder;
        Title = title.Trim();
        Instructions = instructions.Trim();
        DurationMinutes = durationMinutes;
        IsQualityCheckpoint = isQualityCheckpoint;
    }
}
