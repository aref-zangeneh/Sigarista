using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;

namespace Sugarista.Application.Pos.Queries.GetPosOrder;

public record GetPosOrderQuery(Guid OrderId) : IRequest<PosOrderDetailDto>;

public class GetPosOrderQueryHandler : IRequestHandler<GetPosOrderQuery, PosOrderDetailDto>
{
    private readonly IApplicationDbContext _dbContext;

    public GetPosOrderQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PosOrderDetailDto> Handle(GetPosOrderQuery request, CancellationToken cancellationToken)
    {
        var order = await _dbContext.Orders
            .AsNoTracking()
            .Include(o => o.Lines)
            .Include(o => o.SplitShares)
            .Include(o => o.DeliveryMethod)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken)
            ?? throw new KeyNotFoundException("Order was not found.");

        var tableNames = await PosOrderMapper.LoadTableNamesAsync(_dbContext, cancellationToken);
        return PosOrderMapper.ToDetail(order, tableNames);
    }
}
