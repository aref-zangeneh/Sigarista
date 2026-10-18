using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Extensions;
using Sugarista.Application.Common.Models;

namespace Sugarista.Application.Inventory.Queries.GetInventoryPurchases;

public record GetInventoryPurchasesQuery(
    int Page = 1,
    int PageSize = 25,
    string? Search = null,
    Guid? IngredientId = null,
    bool ExpiringOnly = false) : IRequest<PaginatedList<InventoryPurchaseDto>>;

public record InventoryPurchaseDto(
    Guid Id,
    Guid IngredientId,
    string IngredientPersianName,
    string IngredientEnglishName,
    string Unit,
    decimal Quantity,
    int? PackageCount,
    decimal? PackageSize,
    DateOnly ExpirationDate,
    decimal RemainingQuantity,
    decimal? PurchaseUnitPrice,
    decimal ShippingFee,
    decimal TaxAmount,
    decimal IncidentalCost,
    decimal TotalCost,
    string? Description,
    string? DocumentImageUrl,
    DateTimeOffset ReceivedAt,
    string ExpirationStatus,
    Guid? SupplierId = null,
    string? SupplierName = null);

public class GetInventoryPurchasesQueryHandler
    : IRequestHandler<GetInventoryPurchasesQuery, PaginatedList<InventoryPurchaseDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetInventoryPurchasesQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PaginatedList<InventoryPurchaseDto>> Handle(
        GetInventoryPurchasesQuery request,
        CancellationToken cancellationToken)
    {
        var pagination = new PaginationQuery(request.Page, request.PageSize, request.Search);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var query = _dbContext.InventoryPurchases
            .AsNoTracking()
            .Include(p => p.Ingredient)
            .AsQueryable();

        if (request.IngredientId is { } ingredientId)
        {
            query = query.Where(p => p.IngredientId == ingredientId);
        }

        if (request.ExpiringOnly)
        {
            var threshold = today.AddDays(7);
            query = query.Where(p =>
                p.RemainingQuantity > 0 &&
                p.ExpirationDate <= threshold);
        }

        if (!string.IsNullOrWhiteSpace(pagination.Search))
        {
            var search = pagination.Search.Trim().ToLowerInvariant();
            query = query.Where(p =>
                p.Ingredient.PersianName.ToLower().Contains(search) ||
                p.Ingredient.EnglishName.ToLower().Contains(search) ||
                (p.Description != null && p.Description.ToLower().Contains(search)));
        }

        return await query
            .OrderBy(p => p.ExpirationDate)
            .ThenByDescending(p => p.ReceivedAt)
            .Select(p => new InventoryPurchaseDto(
                p.Id,
                p.IngredientId,
                p.Ingredient.PersianName,
                p.Ingredient.EnglishName,
                p.Ingredient.Unit,
                p.Quantity,
                p.PackageCount,
                p.PackageSize,
                p.ExpirationDate,
                p.RemainingQuantity,
                p.PurchaseUnitPrice,
                p.ShippingFee,
                p.TaxAmount,
                p.IncidentalCost,
                (p.PurchaseUnitPrice ?? 0) * p.Quantity + p.ShippingFee + p.TaxAmount + p.IncidentalCost,
                p.Description,
                p.DocumentImageUrl,
                p.ReceivedAt,
                p.ExpirationDate < today
                    ? "Expired"
                    : p.ExpirationDate <= today.AddDays(7)
                        ? "ExpiringSoon"
                        : "Ok",
                p.SupplierId,
                p.Supplier != null ? p.Supplier.PersianName : null))
            .ToPaginatedListAsync(pagination, cancellationToken);
    }
}
