using Sugarista.Domain.Entities;

namespace Sugarista.Application.Tooltips.Dto;

public sealed record AdminTooltipOverrideDto(
    string Key,
    string Text,
    bool IsActive);
