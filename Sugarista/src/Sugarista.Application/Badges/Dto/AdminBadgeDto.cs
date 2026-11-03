using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Badges.Dto;

public record AdminBadgeDto(
    Guid Id,
    string Name,
    string? Description,
    string? ImageUrl,
    int? MinPaidOrders,
    decimal? MinTotalSpent,
    bool IsEnabled,
    int DisplayOrder,
    int Holders);

public static class AdminBadgeMapper
{
    public static AdminBadgeDto ToDto(Badge badge, int holders = 0)
    {
        return new AdminBadgeDto(
            badge.Id,
            badge.Name,
            badge.Description,
            badge.ImageUrl,
            badge.MinPaidOrders,
            badge.MinTotalSpent,
            badge.IsEnabled,
            badge.DisplayOrder,
            holders);
    }
}
