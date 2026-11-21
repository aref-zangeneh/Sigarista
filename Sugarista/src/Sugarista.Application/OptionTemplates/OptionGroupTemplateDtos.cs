using Sugarista.Application.MenuItems.OptionGroups;

namespace Sugarista.Application.OptionTemplates;

public record OptionGroupTemplateDto(
    Guid Id,
    string? CategoryLabel,
    string PersianName,
    string? EnglishName,
    bool IsRequired,
    string SelectionMode,
    int MinSelections,
    int MaxSelections,
    int DisplayOrder,
    bool IsActive,
    int LinkedMenuItemCount,
    IReadOnlyCollection<Guid> LinkedMenuItemIds,
    IReadOnlyCollection<AdminMenuItemOptionDto> Options);

public record OptionGroupTemplatesResponseDto(
    IReadOnlyCollection<OptionGroupTemplateDto> Templates,
    IReadOnlyCollection<string> Categories);

public record ApplyOptionGroupTemplateResultDto(
    int AddedCount,
    int UpdatedCount,
    int SkippedCount);
