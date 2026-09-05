using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Menu.Queries.GetPublicMenu;
using Sugarista.Application.Salon.Queries.GetSalonStatus;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Pos.Queries.GetPosBootstrap;

public record GetPosBootstrapQuery : IRequest<PosBootstrapDto>;

public class GetPosBootstrapQueryHandler : IRequestHandler<GetPosBootstrapQuery, PosBootstrapDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ISender _sender;
    private readonly IDateTime _dateTime;

    public GetPosBootstrapQueryHandler(
        IApplicationDbContext dbContext,
        ISender sender,
        IDateTime dateTime)
    {
        _dbContext = dbContext;
        _sender = sender;
        _dateTime = dateTime;
    }

    public async Task<PosBootstrapDto> Handle(GetPosBootstrapQuery request, CancellationToken cancellationToken)
    {
        var menu = await _sender.Send(new GetPublicMenuQuery(), cancellationToken);
        var salon = await _sender.Send(new GetSalonStatusQuery(), cancellationToken);
        var now = _dateTime.UtcNow;

        var openBalances = await _dbContext.Orders
            .AsNoTracking()
            .Where(order =>
                order.Channel == OrderChannel.Pos &&
                order.TableSessionId != null &&
                order.Status != OrderStatus.Cancelled &&
                order.Status != OrderStatus.Failed &&
                order.Status != OrderStatus.Paid)
            .GroupBy(order => order.TableSessionId!.Value)
            .Select(group => new
            {
                SessionId = group.Key,
                Balance = group.Sum(order => order.TotalAmount - order.AmountPaid)
            })
            .ToDictionaryAsync(entry => entry.SessionId, entry => entry.Balance, cancellationToken);

        var tables = salon.Tables
            .Select(table => new PosTableDto(
                table.Id,
                table.Name,
                table.Capacity,
                table.DisplayOrder,
                table.IsActive,
                table.IsOccupied,
                table.ActiveSessionId,
                table.PeopleCount,
                table.StartedAt,
                table.DurationMinutes,
                table.Note,
                table.ActiveSessionId.HasValue && openBalances.TryGetValue(table.ActiveSessionId.Value, out var balance)
                    ? balance
                    : 0m))
            .ToArray();

        var tableNames = salon.Tables.ToDictionary(table => table.Id, table => table.Name);

        var heldOrders = await LoadOrderSummariesAsync(
            _dbContext.Orders
                .AsNoTracking()
                .Where(order =>
                    order.Channel == OrderChannel.Pos &&
                    order.Status == OrderStatus.Held),
            tableNames,
            cancellationToken);

        var recentOrders = await LoadOrderSummariesAsync(
            _dbContext.Orders
                .AsNoTracking()
                .Where(order =>
                    order.Channel == OrderChannel.Pos &&
                    order.Status != OrderStatus.Held &&
                    order.CreatedAt >= now.AddDays(-2)),
            tableNames,
            cancellationToken,
            limit: 30);

        return new PosBootstrapDto(menu, tables, heldOrders, recentOrders);
    }

    private static async Task<IReadOnlyCollection<PosOrderSummaryDto>> LoadOrderSummariesAsync(
        IQueryable<Order> query,
        IReadOnlyDictionary<Guid, string> tableNames,
        CancellationToken cancellationToken,
        int limit = 50)
    {
        var orders = await query
            .Include(order => order.Lines)
            .OrderByDescending(order => order.HeldAt ?? order.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return orders
            .Select(order => PosOrderMapper.ToSummary(order, tableNames))
            .ToArray();
    }
}
