using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Orders;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Pos.Commands.CopyPosOrder;

public record CopyPosOrderCommand(Guid SourceOrderId, Guid? ServedByAdminUserId = null) : IRequest<PosOrderDetailDto>;

public class CopyPosOrderCommandHandler : IRequestHandler<CopyPosOrderCommand, PosOrderDetailDto>
{
    private readonly IApplicationDbContext _dbContext;

    public CopyPosOrderCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PosOrderDetailDto> Handle(CopyPosOrderCommand request, CancellationToken cancellationToken)
    {
        var source = await _dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Lines)
            .Include(order => order.SplitShares)
            .FirstOrDefaultAsync(order => order.Id == request.SourceOrderId, cancellationToken)
            ?? throw new KeyNotFoundException("Order was not found.");

        if (source.Channel != OrderChannel.Pos)
        {
            throw new InvalidOperationException("Only POS orders can be copied.");
        }

        if (source.Lines.Count == 0)
        {
            throw new InvalidOperationException("Cannot copy an order without items.");
        }

        var trackingCode = await OrderTrackingCodeGenerator.GenerateAsync(_dbContext, cancellationToken);
        var copy = Order.CreatePosDraft(
            trackingCode,
            source.CustomerName,
            source.CustomerPhone,
            source.Note,
            source.TotalAmount,
            source.ServiceType ?? PosServiceType.TakeAway,
            source.SalonTableId,
            source.TableSessionId,
            request.ServedByAdminUserId,
            source.SplitBillMode,
            source.GuestCount);

        foreach (var line in source.Lines)
        {
            copy.AddLine(new OrderLine(
                copy.Id,
                line.MenuItemId,
                line.PersianName,
                line.EnglishName,
                line.SizeKey,
                line.SizeLabel,
                line.Quantity,
                line.UnitPrice,
                line.ModifierLabels,
                line.PayerKey,
                line.PayerLabel,
                line.BaristaNote));
        }

        foreach (var share in source.SplitShares.OrderBy(entry => entry.SortOrder))
        {
            copy.AddSplitShare(new OrderSplitShare(
                copy.Id,
                share.PayerKey,
                share.PayerLabel,
                share.SharePercentage,
                share.ShareAmount,
                share.SortOrder));
        }

        _dbContext.Orders.Add(copy);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var tableNames = await PosOrderMapper.LoadTableNamesAsync(_dbContext, cancellationToken);
        return PosOrderMapper.ToDetail(copy, tableNames);
    }
}
