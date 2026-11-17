using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public class RecipeItem : BaseAuditableEntity
{
    public RecipeItem()
    {
    }

    public RecipeItem(
        Guid menuItemId,
        Guid ingredientId,
        decimal quantity,
        string? preparationNote = null,
        bool showToCustomer = true)
    {
        Update(menuItemId, ingredientId, quantity, preparationNote, showToCustomer);
    }

    public Guid MenuItemId { get; private set; }

    public MenuItem MenuItem { get; private set; } = null!;

    public Guid IngredientId { get; private set; }

    public Ingredient Ingredient { get; private set; } = null!;

    public decimal Quantity { get; private set; }

    public string? PreparationNote { get; private set; }

    /// <summary>
    /// When true, this content (the ingredient name only, never the internal
    /// quantity/note) is shown to customers in the public digital menu.
    /// </summary>
    public bool ShowToCustomer { get; private set; } = true;

    public void Update(
        Guid menuItemId,
        Guid ingredientId,
        decimal quantity,
        string? preparationNote = null,
        bool showToCustomer = true)
    {
        if (menuItemId == Guid.Empty)
        {
            throw new DomainException("RecipeItem menu item is required.");
        }

        if (ingredientId == Guid.Empty)
        {
            throw new DomainException("RecipeItem ingredient is required.");
        }

        if (quantity <= 0)
        {
            throw new DomainException("RecipeItem quantity must be greater than zero.");
        }

        MenuItemId = menuItemId;
        IngredientId = ingredientId;
        Quantity = quantity;
        PreparationNote = string.IsNullOrWhiteSpace(preparationNote) ? null : preparationNote.Trim();
        ShowToCustomer = showToCustomer;
    }
}
