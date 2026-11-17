using Microsoft.EntityFrameworkCore;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.MenuItems.Commands.DeleteMenuItem;

/// <summary>
/// Deletes a menu item. Configuration attached to the item (recipe, option
/// groups, wiki, drink-builder profile, stock links, discount links) cascades
/// away with it. Items with business history — sold order lines or production
/// batches — are never deleted; they are refused with a clear Persian message
/// so the admin can deactivate the item instead. Existing data is never
/// modified or silently removed by a delete.
/// </summary>
public record DeleteMenuItemCommand(Guid Id) : IRequest;

public class DeleteMenuItemCommandHandler : IRequestHandler<DeleteMenuItemCommand>
{
    private readonly IApplicationDbContext _dbContext;

    public DeleteMenuItemCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(DeleteMenuItemCommand request, CancellationToken cancellationToken)
    {
        var item = await _dbContext.MenuItems
            .FirstOrDefaultAsync(existingItem => existingItem.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Menu item was not found.");

        var hasOrderLines = await _dbContext.OrderLines
            .AsNoTracking()
            .AnyAsync(line => line.MenuItemId == item.Id, cancellationToken);

        if (hasOrderLines)
        {
            throw new DomainException(
                "این آیتم در سفارش‌ها سابقه دارد و حذف آن ممکن نیست؛ آیتم را غیرفعال کنید تا از منو و صندوق پنهان شود.");
        }

        var hasProductionBatches = await _dbContext.ProductionBatches
            .AsNoTracking()
            .AnyAsync(batch => batch.MenuItemId == item.Id, cancellationToken);

        if (hasProductionBatches)
        {
            throw new DomainException(
                "این آیتم در دستورهای تولید سابقه دارد و حذف آن ممکن نیست؛ آیتم را غیرفعال کنید.");
        }

        _dbContext.MenuItems.Remove(item);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
