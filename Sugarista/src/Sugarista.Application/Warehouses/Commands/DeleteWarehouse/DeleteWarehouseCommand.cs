using MediatR;
using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.Warehouses.Commands.DeleteWarehouse;

/// <summary>
/// Deletes a warehouse only when nothing references it: no انبارک children,
/// no stock rows, no transactions. Referenced warehouses should be
/// deactivated instead — existing data is never modified by a delete.
/// </summary>
public record DeleteWarehouseCommand(Guid Id) : IRequest;

public class DeleteWarehouseCommandHandler
    : IRequestHandler<DeleteWarehouseCommand>
{
    private readonly IApplicationDbContext _dbContext;

    public DeleteWarehouseCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(DeleteWarehouseCommand request, CancellationToken cancellationToken)
    {
        var warehouse = await _dbContext.Warehouses
            .FirstOrDefaultAsync(w => w.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Warehouse was not found.");

        var hasChildren = await _dbContext.Warehouses
            .AsNoTracking()
            .AnyAsync(w => w.ParentId == warehouse.Id, cancellationToken);

        if (hasChildren)
        {
            throw new DomainException("ابتدا انبارک‌های زیرمجموعه را حذف کنید.");
        }

        var hasStock = await _dbContext.WarehouseStocks
            .AsNoTracking()
            .AnyAsync(s => s.WarehouseId == warehouse.Id, cancellationToken);

        if (hasStock)
        {
            throw new DomainException("این انبار موجودی دارد؛ ابتدا موجودی را تعدیل یا منتقل کنید، یا انبار را غیرفعال کنید.");
        }

        var hasMovements = await _dbContext.StockMovements
            .AsNoTracking()
            .AnyAsync(m => m.WarehouseId == warehouse.Id, cancellationToken);

        if (hasMovements)
        {
            throw new DomainException("این انبار تراکنش ثبت‌شده دارد و حذف آن مجاز نیست؛ انبار را غیرفعال کنید.");
        }

        _dbContext.Warehouses.Remove(warehouse);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
