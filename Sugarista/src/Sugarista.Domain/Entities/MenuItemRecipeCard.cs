using System.Text.Json;
using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// Optional visual Recipe Card metadata attached 1-to-1 to a <see cref="MenuItem"/>.
/// Stored in its own additive table so existing <see cref="MenuItem.RecipeDescription"/>
/// and <see cref="RecipeItem"/> ingredient quantities remain 100% untouched.
/// </summary>
public class MenuItemRecipeCard : BaseAuditableEntity
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private MenuItemRecipeCard()
    {
    }

    public MenuItemRecipeCard(
        Guid menuItemId,
        string? cardImageUrl,
        IEnumerable<string>? bulletPoints,
        string? servingTemp = null,
        string? glasswareOrPlate = null,
        int? prepSeconds = null,
        string? baristaTip = null)
    {
        if (menuItemId == Guid.Empty)
        {
            throw new DomainException("شناسه آیتم منو الزامی است.");
        }

        MenuItemId = menuItemId;
        Update(cardImageUrl, bulletPoints, servingTemp, glasswareOrPlate, prepSeconds, baristaTip);
    }

    public Guid MenuItemId { get; private set; }

    public MenuItem MenuItem { get; private set; } = null!;

    public string? CardImageUrl { get; private set; }

    public string BulletPointsJson { get; private set; } = "[]";

    public string? ServingTemp { get; private set; }

    public string? GlasswareOrPlate { get; private set; }

    public int? PrepSeconds { get; private set; }

    public string? BaristaTip { get; private set; }

    public void Update(
        string? cardImageUrl,
        IEnumerable<string>? bulletPoints,
        string? servingTemp,
        string? glasswareOrPlate,
        int? prepSeconds,
        string? baristaTip)
    {
        if (prepSeconds.HasValue && prepSeconds.Value < 0)
        {
            throw new DomainException("زمان آماده‌سازی نمی‌تواند منفی باشد.");
        }

        CardImageUrl = TrimMax(cardImageUrl, 500);
        ServingTemp = TrimMax(servingTemp, 40);
        GlasswareOrPlate = TrimMax(glasswareOrPlate, 80);
        PrepSeconds = prepSeconds is > 0 ? Math.Min(prepSeconds.Value, 86400) : null;
        BaristaTip = TrimMax(baristaTip, 400);

        var cleanedBullets = (bulletPoints ?? [])
            .Select(b => (b ?? string.Empty).Trim())
            .Where(b => b.Length > 0)
            .Select(b => b.Length > 260 ? b[..260] : b)
            .Take(25)
            .ToList();

        BulletPointsJson = JsonSerializer.Serialize(cleanedBullets, JsonOptions);
    }

    public IReadOnlyList<string> GetBulletPoints()
    {
        if (string.IsNullOrWhiteSpace(BulletPointsJson))
        {
            return [];
        }

        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(BulletPointsJson, JsonOptions);
            return list ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string? TrimMax(string? value, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length > maxLen ? trimmed[..maxLen] : trimmed;
    }
}
