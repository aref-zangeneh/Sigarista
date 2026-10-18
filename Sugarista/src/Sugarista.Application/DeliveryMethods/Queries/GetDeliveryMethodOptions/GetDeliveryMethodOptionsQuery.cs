using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.DeliveryMethods.Queries.GetDeliveryMethodOptions;

public record GetDeliveryMethodOptionsQuery : IRequest<IReadOnlyCollection<DeliveryMethodOptionDto>>;

public record DeliveryMethodOptionDto(
    Guid Id,
    string PersianName,
    string EnglishName,
    decimal DeliveryFee);

public class GetDeliveryMethodOptionsQueryHandler : IRequestHandler<GetDeliveryMethodOptionsQuery, IReadOnlyCollection<DeliveryMethodOptionDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetDeliveryMethodOptionsQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<DeliveryMethodOptionDto>> Handle(
        GetDeliveryMethodOptionsQuery request,
        CancellationToken cancellationToken)
    {
        return await _dbContext.DeliveryMethods
            .AsNoTracking()
            .Where(method => method.IsActive)
            .OrderBy(method => method.DisplayOrder)
            .ThenBy(method => method.PersianName)
            .Select(method => new DeliveryMethodOptionDto(
                method.Id,
                method.PersianName,
                method.EnglishName,
                method.DeliveryFee))
            .ToListAsync(cancellationToken);
    }
}
