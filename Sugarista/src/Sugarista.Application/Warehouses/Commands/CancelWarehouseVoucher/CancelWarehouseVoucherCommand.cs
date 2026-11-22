using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Warehouses.Dto;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.Warehouses.Commands.CancelWarehouseVoucher;

public record CancelWarehouseVoucherCommand(
    Guid Id,
    string Reason) : IRequest<WarehouseVoucherDto>;

public class CancelWarehouseVoucherCommandValidator : AbstractValidator<CancelWarehouseVoucherCommand>
{
    public CancelWarehouseVoucherCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.Reason)
            .NotEmpty()
            .WithMessage("ثبت علت ابطال سند انبار الزامی است.")
            .MaximumLength(WarehouseVoucher.MaxCancellationReasonLength);
    }
}

public class CancelWarehouseVoucherCommandHandler
    : IRequestHandler<CancelWarehouseVoucherCommand, WarehouseVoucherDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public CancelWarehouseVoucherCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<WarehouseVoucherDto> Handle(
        CancelWarehouseVoucherCommand request,
        CancellationToken cancellationToken)
    {
        var voucher = await _dbContext.WarehouseVouchers
            .Include(v => v.Warehouse)
            .Include(v => v.Supplier)
            .Include(v => v.Lines)
                .ThenInclude(l => l.Ingredient)
            .FirstOrDefaultAsync(v => v.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("سند انبار مورد نظر یافت نشد.");

        if (voucher.Status == WarehouseVoucherStatus.Cancelled)
        {
            throw new DomainException("این سند قبلاً ابطال شده است.");
        }

        var ingredientIds = voucher.Lines.Select(l => l.IngredientId).Distinct().ToList();

        var mainStocksById = await _dbContext.InventoryStocks
            .Where(s => ingredientIds.Contains(s.IngredientId))
            .ToDictionaryAsync(s => s.IngredientId, cancellationToken);

        foreach (var ingId in ingredientIds)
        {
            if (!mainStocksById.ContainsKey(ingId))
            {
                var createdMain = new InventoryStock(ingId);
                _dbContext.InventoryStocks.Add(createdMain);
                mainStocksById[ingId] = createdMain;
            }
        }

        var warehouseStocksById = voucher.WarehouseId is { } whId
            ? await _dbContext.WarehouseStocks
                .Where(s => s.WarehouseId == whId && ingredientIds.Contains(s.IngredientId))
                .ToDictionaryAsync(s => s.IngredientId, cancellationToken)
            : new Dictionary<Guid, WarehouseStock>();

        // Reversing a Receipt deducts the received quantities back out; validate enough stock exists.
        if (voucher.VoucherType == WarehouseVoucherType.Receipt)
        {
            var warehouseName = voucher.Warehouse?.Name ?? WarehouseVoucherMapper.MainWarehouseDisplayName;
            foreach (var grp in voucher.Lines.GroupBy(l => l.IngredientId))
            {
                var needed = grp.Sum(x => x.Quantity);
                var available = voucher.WarehouseId.HasValue
                    ? (warehouseStocksById.TryGetValue(grp.Key, out var ws) ? ws.Quantity : 0m)
                    : mainStocksById[grp.Key].CurrentQuantity;

                if (available < needed)
                {
                    var name = grp.First().Ingredient?.PersianName ?? "ماده اولیه";
                    var unit = grp.First().Ingredient?.Unit ?? "";
                    throw new DomainException(
                        $"ابطال رسید امکان‌پذیر نیست؛ موجودی فعلی «{name}» در «{warehouseName}» ({available:0.###} {unit}) کمتر از مقدار رسید ({needed:0.###} {unit}) است.");
                }
            }
        }

        var reversalNote = $"ابطال سند {voucher.VoucherNumber} — {request.Reason.Trim()}";
        if (reversalNote.Length > 500)
        {
            reversalNote = reversalNote[..500];
        }

        // Receipt reversal -> negative delta; Issue reversal -> positive delta
        var directionSign = voucher.VoucherType == WarehouseVoucherType.Receipt ? -1m : 1m;

        foreach (var line in voucher.Lines)
        {
            var delta = directionSign * line.Quantity;

            if (voucher.WarehouseId is { } targetWhId)
            {
                if (!warehouseStocksById.TryGetValue(line.IngredientId, out var whStock))
                {
                    whStock = new WarehouseStock(targetWhId, line.IngredientId);
                    _dbContext.WarehouseStocks.Add(whStock);
                    warehouseStocksById[line.IngredientId] = whStock;
                }

                var movement = whStock.ReverseVoucher(delta, reversalNote);
                _dbContext.StockMovements.Add(movement);
            }
            else
            {
                var mainStock = mainStocksById[line.IngredientId];
                var movement = mainStock.ReverseVoucher(delta, reversalNote);
                _dbContext.StockMovements.Add(movement);
            }
        }

        var actorName = await ResolveActorNameAsync(_currentUserService.UserId, cancellationToken);
        voucher.Cancel(request.Reason, actorName);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return WarehouseVoucherMapper.ToDto(voucher);
    }

    private async Task<string?> ResolveActorNameAsync(string? rawUserId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawUserId))
        {
            return null;
        }

        if (Guid.TryParse(rawUserId, out var adminUserId))
        {
            var adminUser = await _dbContext.AdminUsers
                .AsNoTracking()
                .Where(u => u.Id == adminUserId)
                .Select(u => new { u.DisplayName, u.Username })
                .FirstOrDefaultAsync(cancellationToken);

            if (adminUser is not null)
            {
                return string.IsNullOrWhiteSpace(adminUser.DisplayName)
                    ? adminUser.Username
                    : adminUser.DisplayName;
            }
        }

        return rawUserId;
    }
}
