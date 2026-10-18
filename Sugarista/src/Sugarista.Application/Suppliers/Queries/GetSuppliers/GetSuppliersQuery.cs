namespace Sugarista.Application.Suppliers.Queries.GetSuppliers;

public record GetSuppliersQuery(bool IncludeInactive = false) : IRequest<IReadOnlyCollection<SupplierDto>>;

public record SupplierDto(
    Guid Id,
    string PersianName,
    string PhoneNumber,
    string ContactName,
    string Notes,
    int DisplayOrder,
    bool IsActive,
    int IngredientCount);

public class GetSuppliersQueryHandler : IRequestHandler<GetSuppliersQuery, IReadOnlyCollection<SupplierDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetSuppliersQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<SupplierDto>> Handle(GetSuppliersQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.Suppliers.AsNoTracking();

        if (!request.IncludeInactive)
        {
            query = query.Where(s => s.IsActive);
        }

        return await query
            .OrderBy(s => s.DisplayOrder)
            .ThenBy(s => s.PersianName)
            .Select(s => new SupplierDto(
                s.Id,
                s.PersianName,
                s.PhoneNumber,
                s.ContactName,
                s.Notes,
                s.DisplayOrder,
                s.IsActive,
                s.IngredientSuppliers.Count))
            .ToListAsync(cancellationToken);
    }
}
