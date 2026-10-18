using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.DeliveryMethods.Queries.GetDeliveryMethods;

public record GetDeliveryMethodsQuery(bool IncludeInactive = true) : IRequest<IReadOnlyCollection<DeliveryMethodDto>>;

public record DeliveryMethodDto(
    Guid Id,
    string PersianName,
    string EnglishName,
    string? Description,
    decimal DeliveryFee,
    int DisplayOrder,
    bool IsActive,
    IReadOnlyCollection<DeliveryMethodPackagingLineDto> Packagings);

public record DeliveryMethodPackagingLineDto(
    Guid Id,
    Guid IngredientId,
    string IngredientPersianName,
    string IngredientEnglishName,
    string Unit,
    decimal QuantityPerOrder);

public class GetDeliveryMethodsQueryHandler : IRequestHandler<GetDeliveryMethodsQuery, IReadOnlyCollection<DeliveryMethodDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetDeliveryMethodsQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<DeliveryMethodDto>> Handle(
        GetDeliveryMethodsQuery request,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.DeliveryMethods
            .AsNoTracking()
            .Include(method => method.Packagings)
            .ThenInclude(line => line.Ingredient)
            .AsQueryable();

        if (!request.IncludeInactive)
        {
            query = query.Where(method => method.IsActive);
        }

        var methods = await query
            .OrderBy(method => method.DisplayOrder)
            .ThenBy(method => method.PersianName)
            .ToListAsync(cancellationToken);

        return methods.Select(method => new DeliveryMethodDto(
            method.Id,
            method.PersianName,
            method.EnglishName,
            method.Description,
            method.DeliveryFee,
            method.DisplayOrder,
            method.IsActive,
            method.Packagings
                .OrderBy(line => line.Ingredient.PersianName)
                .Select(line => new DeliveryMethodPackagingLineDto(
                    line.Id,
                    line.IngredientId,
                    line.Ingredient.PersianName,
                    line.Ingredient.EnglishName,
                    line.Ingredient.Unit,
                    line.QuantityPerOrder))
                .ToList()))
            .ToList();
    }
}
