using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Warehouses.Dto;

namespace Sugarista.Application.Warehouses.Queries.GetWarehouseVoucherById;

public record GetWarehouseVoucherByIdQuery(Guid Id) : IRequest<WarehouseVoucherDto?>;

public class GetWarehouseVoucherByIdQueryHandler
    : IRequestHandler<GetWarehouseVoucherByIdQuery, WarehouseVoucherDto?>
{
    private readonly IApplicationDbContext _dbContext;

    public GetWarehouseVoucherByIdQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<WarehouseVoucherDto?> Handle(
        GetWarehouseVoucherByIdQuery request,
        CancellationToken cancellationToken)
    {
        var voucher = await _dbContext.WarehouseVouchers
            .AsNoTracking()
            .Include(v => v.Warehouse)
            .Include(v => v.Supplier)
            .Include(v => v.Lines)
                .ThenInclude(l => l.Ingredient)
            .FirstOrDefaultAsync(v => v.Id == request.Id, cancellationToken);

        return voucher is null ? null : WarehouseVoucherMapper.ToDto(voucher);
    }
}
