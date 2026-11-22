using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Warehouses.Dto;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.Warehouses.Commands.TransferWarehouseStock;

public record TransferWarehouseStockLineInput(Guid IngredientId, decimal Quantity);

/// <summary>
/// Atomically transfers one or more ingredients between two stock locations:
/// Main Inventory (<c>null</c>) and/or any active admin-defined warehouse or
/// sub-warehouse (<see cref="Warehouse"/>). Records matching <see cref="StockMovement"/>
/// pairs (TransferOut / TransferIn) plus an immutable <see cref="WarehouseTransfer"/> slip.
/// </summary>
public record TransferWarehouseStockCommand(
    Guid? SourceWarehouseId,
    Guid? DestinationWarehouseId,
    IReadOnlyCollection<TransferWarehouseStockLineInput> Lines,
    string? Note = null) : IRequest<WarehouseTransferDto>;

public class TransferWarehouseStockCommandValidator : AbstractValidator<TransferWarehouseStockCommand>
{
    public TransferWarehouseStockCommandValidator()
    {
        RuleFor(c => c.Lines)
            .NotEmpty()
            .WithMessage("حداقل یک ماده اولیه برای انتقال انتخاب کنید.");

        RuleFor(c => c.Note)
            .MaximumLength(WarehouseTransfer.MaxNoteLength)
            .WithMessage($"توضیحات انتقال حداکثر {WarehouseTransfer.MaxNoteLength} کاراکتر می‌تواند باشد.");

        RuleForEach(c => c.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.IngredientId)
                .NotEmpty()
                .WithMessage("انتخاب ماده اولیه الزامی است.");
            line.RuleFor(l => l.Quantity)
                .GreaterThan(0)
                .WithMessage("مقدار انتقال هر قلم باید بزرگ‌تر از صفر باشد.");
        });
    }
}

public class TransferWarehouseStockCommandHandler
    : IRequestHandler<TransferWarehouseStockCommand, WarehouseTransferDto>
{
    public const string MainWarehouseDisplayName = "انبار اصلی (موجودی مرکزی)";

    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public TransferWarehouseStockCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<WarehouseTransferDto> Handle(
        TransferWarehouseStockCommand request,
        CancellationToken cancellationToken)
    {
        if (request.SourceWarehouseId == request.DestinationWarehouseId)
        {
            throw new DomainException("انبار مبدا و مقصد نمی‌توانند یکسان باشند.");
        }

        if (request.Lines is null || request.Lines.Count == 0)
        {
            throw new DomainException("حداقل یک ماده اولیه برای انتقال انتخاب کنید.");
        }

        // Aggregate duplicate ingredient rows in the same slip so insufficient-stock
        // checks always validate the true total quantity being moved out of source.
        var aggregatedLines = request.Lines
            .GroupBy(l => l.IngredientId)
            .Select(g => new { IngredientId = g.Key, Quantity = g.Sum(x => x.Quantity) })
            .ToList();

        if (aggregatedLines.Any(l => l.IngredientId == Guid.Empty || l.Quantity <= 0))
        {
            throw new DomainException("تمامی اقلام انتقال باید ماده اولیه معتبر و مقدار بزرگ‌تر از صفر داشته باشند.");
        }

        string sourceName = MainWarehouseDisplayName;
        string destinationName = MainWarehouseDisplayName;

        if (request.SourceWarehouseId is { } srcId)
        {
            var srcWarehouse = await _dbContext.Warehouses
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.Id == srcId, cancellationToken)
                ?? throw new DomainException("انبار مبدا یافت نشد.");

            if (!srcWarehouse.IsActive)
            {
                throw new DomainException($"انبار مبدا «{srcWarehouse.Name}» غیرفعال است و انتقال از آن مجاز نیست.");
            }

            sourceName = srcWarehouse.Name;
        }

        if (request.DestinationWarehouseId is { } dstId)
        {
            var dstWarehouse = await _dbContext.Warehouses
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.Id == dstId, cancellationToken)
                ?? throw new DomainException("انبار مقصد یافت نشد.");

            if (!dstWarehouse.IsActive)
            {
                throw new DomainException($"انبار مقصد «{dstWarehouse.Name}» غیرفعال است و انتقال به آن مجاز نیست.");
            }

            destinationName = dstWarehouse.Name;
        }

        var ingredientIds = aggregatedLines.Select(l => l.IngredientId).ToList();
        var ingredientsById = await _dbContext.Ingredients
            .AsNoTracking()
            .Where(i => ingredientIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, cancellationToken);

        if (ingredientsById.Count != ingredientIds.Count)
        {
            throw new DomainException("یکی از مواد اولیه انتخاب‌شده یافت نشد.");
        }

        // Ensure every involved ingredient has a main InventoryStock row because
        // StockMovement.IngredientId has a FK to InventoryStocks.IngredientId.
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

        var warehouseIdsToLoad = new[] { request.SourceWarehouseId, request.DestinationWarehouseId }
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var warehouseStocksList = warehouseIdsToLoad.Count > 0
            ? await _dbContext.WarehouseStocks
                .Where(s => warehouseIdsToLoad.Contains(s.WarehouseId) && ingredientIds.Contains(s.IngredientId))
                .ToListAsync(cancellationToken)
            : [];

        var warehouseStocksByKey = warehouseStocksList
            .ToDictionary(s => (s.WarehouseId, s.IngredientId));

        var transferredBy = await ResolveActorNameAsync(_currentUserService.UserId, cancellationToken);
        var referenceNumber = await GenerateUniqueReferenceAsync(cancellationToken);
        var transfer = new WarehouseTransfer(
            referenceNumber,
            request.SourceWarehouseId,
            request.DestinationWarehouseId,
            request.Note,
            transferredBy);

        var noteSuffix = string.IsNullOrWhiteSpace(request.Note) ? string.Empty : $" — {request.Note.Trim()}";
        var outNote = $"حواله {referenceNumber} | انتقال به {destinationName}{noteSuffix}";
        var inNote = $"حواله {referenceNumber} | انتقال از {sourceName}{noteSuffix}";

        var lineDtos = new List<WarehouseTransferLineDto>(aggregatedLines.Count);

        foreach (var line in aggregatedLines)
        {
            var ingredient = ingredientsById[line.IngredientId];
            decimal sourceBefore;
            decimal sourceAfter;
            decimal destBefore;
            decimal destAfter;

            // 1. Deduct from source
            if (request.SourceWarehouseId is { } sourceWhId)
            {
                if (!warehouseStocksByKey.TryGetValue((sourceWhId, line.IngredientId), out var srcStock) ||
                    srcStock.Quantity < line.Quantity)
                {
                    var available = warehouseStocksByKey.TryGetValue((sourceWhId, line.IngredientId), out var existing)
                        ? existing.Quantity
                        : 0m;
                    throw new DomainException(
                        $"موجودی «{ingredient.PersianName}» در «{sourceName}» کافی نیست. موجود: {available:0.###} {ingredient.Unit} — درخواست: {line.Quantity:0.###} {ingredient.Unit}.");
                }

                sourceBefore = srcStock.Quantity;
                var outMovement = srcStock.TransferOut(line.Quantity, outNote);
                sourceAfter = srcStock.Quantity;
                _dbContext.StockMovements.Add(outMovement);
            }
            else
            {
                var srcMain = mainStocksById[line.IngredientId];
                if (srcMain.CurrentQuantity < line.Quantity)
                {
                    throw new DomainException(
                        $"موجودی «{ingredient.PersianName}» در «{sourceName}» کافی نیست. موجود: {srcMain.CurrentQuantity:0.###} {ingredient.Unit} — درخواست: {line.Quantity:0.###} {ingredient.Unit}.");
                }

                sourceBefore = srcMain.CurrentQuantity;
                var outMovement = srcMain.TransferOut(line.Quantity, outNote);
                sourceAfter = srcMain.CurrentQuantity;
                _dbContext.StockMovements.Add(outMovement);
            }

            // 2. Credit destination (auto-create WarehouseStock row when needed)
            if (request.DestinationWarehouseId is { } destWhId)
            {
                if (!warehouseStocksByKey.TryGetValue((destWhId, line.IngredientId), out var dstStock))
                {
                    dstStock = new WarehouseStock(destWhId, line.IngredientId);
                    _dbContext.WarehouseStocks.Add(dstStock);
                    warehouseStocksByKey[(destWhId, line.IngredientId)] = dstStock;
                }

                destBefore = dstStock.Quantity;
                var inMovement = dstStock.TransferIn(line.Quantity, inNote);
                destAfter = dstStock.Quantity;
                _dbContext.StockMovements.Add(inMovement);
            }
            else
            {
                var dstMain = mainStocksById[line.IngredientId];
                destBefore = dstMain.CurrentQuantity;
                var inMovement = dstMain.TransferIn(line.Quantity, inNote);
                destAfter = dstMain.CurrentQuantity;
                _dbContext.StockMovements.Add(inMovement);
            }

            var transferLine = transfer.AddLine(
                line.IngredientId,
                line.Quantity,
                sourceBefore,
                sourceAfter,
                destBefore,
                destAfter);

            lineDtos.Add(new WarehouseTransferLineDto(
                transferLine.Id,
                ingredient.Id,
                ingredient.PersianName,
                ingredient.EnglishName,
                ingredient.Unit,
                line.Quantity,
                sourceBefore,
                sourceAfter,
                destBefore,
                destAfter));
        }

        _dbContext.WarehouseTransfers.Add(transfer);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new WarehouseTransferDto(
            transfer.Id,
            transfer.ReferenceNumber,
            transfer.SourceWarehouseId,
            sourceName,
            transfer.DestinationWarehouseId,
            destinationName,
            transfer.Note,
            transfer.TransferredBy,
            transfer.TransferredAt,
            lineDtos);
    }

    private async Task<string?> ResolveActorNameAsync(string? userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        if (Guid.TryParse(userId, out var adminId))
        {
            var admin = await _dbContext.AdminUsers
                .AsNoTracking()
                .Where(u => u.Id == adminId)
                .Select(u => new { u.DisplayName, u.Username })
                .FirstOrDefaultAsync(cancellationToken);

            if (admin is not null)
            {
                return string.IsNullOrWhiteSpace(admin.DisplayName) ? admin.Username : admin.DisplayName;
            }
        }

        return userId;
    }

    private async Task<string> GenerateUniqueReferenceAsync(CancellationToken cancellationToken)
    {
        var dateStamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd");
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
            var candidate = $"TRF-{dateStamp}-{suffix}";
            var exists = await _dbContext.WarehouseTransfers
                .AsNoTracking()
                .AnyAsync(t => t.ReferenceNumber == candidate, cancellationToken);
            if (!exists)
            {
                return candidate;
            }
        }

        return $"TRF-{dateStamp}-{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";
    }
}
