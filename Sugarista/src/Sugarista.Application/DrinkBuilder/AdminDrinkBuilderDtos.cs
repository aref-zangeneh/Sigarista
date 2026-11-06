using Sugarista.Domain.Entities;

namespace Sugarista.Application.DrinkBuilder;

public sealed record AdminDrinkBuilderProfileDto(
    Guid Id,
    Guid MenuItemId,
    string MenuItemPersianName,
    string Name,
    bool IsEnabled,
    int DisplayOrder,
    IReadOnlyCollection<AdminDrinkBuilderGroupDto> Groups,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastModifiedAt);

public sealed record AdminDrinkBuilderGroupDto(
    Guid Id,
    string Name,
    string InputType,
    string SelectionMode,
    int MinSelections,
    int MaxSelections,
    bool IsRequired,
    int DisplayOrder,
    IReadOnlyCollection<AdminDrinkBuilderOptionDto> Options);

public sealed record AdminDrinkBuilderOptionDto(
    Guid Id,
    string Name,
    string? EnglishName,
    string Unit,
    decimal PricePerUnit,
    decimal MinAmount,
    decimal MaxAmount,
    decimal Step,
    decimal DefaultAmount,
    bool IsDefault,
    bool IsActive,
    int DisplayOrder);

public sealed record AdminDrinkBuilderSettingsDto(bool IsEnabled);

internal static class DrinkBuilderAdminMapper
{
    internal static AdminDrinkBuilderProfileDto ToDto(DrinkBuilderProfile profile) =>
        new(
            profile.Id,
            profile.MenuItemId,
            profile.MenuItem?.PersianName ?? string.Empty,
            profile.Name,
            profile.IsEnabled,
            profile.DisplayOrder,
            profile.Groups
                .OrderBy(group => group.DisplayOrder)
                .ThenBy(group => group.Name, StringComparer.Ordinal)
                .Select(group => new AdminDrinkBuilderGroupDto(
                    group.Id,
                    group.Name,
                    group.InputType.ToString(),
                    group.SelectionMode.ToString(),
                    group.MinSelections,
                    group.MaxSelections,
                    group.IsRequired,
                    group.DisplayOrder,
                    group.Options
                        .OrderBy(option => option.DisplayOrder)
                        .ThenBy(option => option.Name, StringComparer.Ordinal)
                        .Select(option => new AdminDrinkBuilderOptionDto(
                            option.Id,
                            option.Name,
                            option.EnglishName,
                            option.Unit.ToString(),
                            option.PricePerUnit,
                            option.MinAmount,
                            option.MaxAmount,
                            option.Step,
                            option.DefaultAmount,
                            option.IsDefault,
                            option.IsActive,
                            option.DisplayOrder))
                        .ToList()))
                .ToList(),
            profile.CreatedAt,
            profile.LastModifiedAt);
}
