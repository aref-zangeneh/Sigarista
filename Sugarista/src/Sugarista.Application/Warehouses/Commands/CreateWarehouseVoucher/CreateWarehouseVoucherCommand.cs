using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Warehouses.Dto;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.Warehouses.Commands.CreateWarehouseVoucher;

public record CreateWarehouseVoucherLineInput(
    Guid IngredientId,
    decimal Quantity,
    decimal? UnitPrice = null,
    string? BatchNumber = null,
    DateOnly? ExpirationDate = null,
    string? LineNote = null);

public record CreateWarehouseVoucherCommand(
    string VoucherType,
    string Reason,
    Guid? WarehouseId,
    IReadOnlyCollection<CreateWarehouseVoucherLineInput> Lines,
    Guid? SupplierId = null,
    string? CounterpartyName = null,
    string? ExternalReference = null,
    DateTimeOffset? VoucherDate = null,
    string? Note = null) : IRequest<WarehouseVoucherDto>;

public class CreateWarehouseVoucherCommandValidator : AbstractValidator<CreateWarehouseVoucherCommand>
{
    public CreateWarehouseVoucherCommandValidator()
    {
        RuleFor(c => c.VoucherType)
            .NotEmpty()
            .Must(v => Enum.TryParse<WarehouseVoucherType>(v, true, out _))
            .WithMessage("نوع سند انبار باید رسید ورود (Receipt) یا سند خروج (Issue) باشد.");

        RuleFor(c => c.Reason)
            .NotEmpty()
            .Must(v => Enum.TryParse<WarehouseVoucherReason>(v, true, out _))
            .WithMessage("دلیل سند انبار نامعتبر است.");

        RuleFor(c => c.Lines)
            .NotEmpty()
            .WithMessage("حداقل یک قلم کالا برای صدور سند انبار الزامی است.");

        RuleFor(c => c.CounterpartyName)
            .MaximumLength(WarehouseVoucher.MaxCounterpartyLength);

        RuleFor(c => c.ExternalReference)
            .MaximumLength(WarehouseVoucher.MaxExternalReferenceLength);

        RuleFor(c => c.Note)
            .MaximumLength(WarehouseVoucher.MaxNoteLength);

        RuleForEach(c => c.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.IngredientId)
                .NotEmpty()
                .WithMessage("انتخاب ماده اولیه الزامی است.");
            line.RuleFor(l => l.Quantity)
                .GreaterThan(0)
                .WithMessage("مقدار هر قلم باید بزرگ‌تر از صفر باشد.");
            line.RuleFor(l => l.UnitPrice)
                .GreaterThanOrEqualTo(0)
                .When(l => l.UnitPrice.HasValue)
                .WithMessage("قیمت واحد نمی‌تواند منفی باشد.");
            line.RuleFor(l => l.BatchNumber)
                .MaximumLength(WarehouseVoucher.MaxBatchNumberLength);
            line.RuleFor(l => l.LineNote)
                .MaximumLength(WarehouseVoucher.MaxLineNoteLength);
        });
    }
}

public class CreateWarehouseVoucherCommandHandler
    : IRequestHandler<CreateWarehouseVoucherCommand, WarehouseVoucherDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public CreateWarehouseVoucherCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<WarehouseVoucherDto> Handle(
        CreateWarehouseVoucherCommand request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<WarehouseVoucherType>(request.VoucherType, true, out var voucherType))
        {
            throw new DomainException("نوع سند انبار نامعتبر است.");
        }

        if (!Enum.TryParse<WarehouseVoucherReason>(request.Reason, true, out var reason))
        {
            throw new DomainException("دلیل سند انبار نامعتبر است.");
        }

        if (!WarehouseVoucher.IsReasonValidForType(voucherType, reason))
        {
            throw new DomainException("دلیل انتخاب‌شده با نوع سند انبار هم‌خوانی ندارد.");
        }

        if (request.Lines is null || request.Lines.Count == 0)
        {
            throw new DomainException("حداقل یک قلم کالا برای صدور سند انبار الزامی است.");
        }

        string warehouseName = WarehouseVoucherMapper.MainWarehouseDisplayName;
        if (request.WarehouseId is { } whId)
        {
            var warehouse = await _dbContext.Warehouses
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.Id == whId, cancellationToken)
                ?? throw new DomainException("انبار انتخاب‌شده یافت نشد.");

            if (!warehouse.IsActive)
            {
                throw new DomainException($"انبار «{warehouse.Name}» غیرفعال است و صدور سند برای آن مجاز نیست.");
            }

            warehouseName = warehouse.Name;
        }

        string? supplierName = null;
        if (request.SupplierId is { } supId)
        {
            var supplier = await _dbContext.Suppliers
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == supId, cancellationToken)
                ?? throw new DomainException("تأمین‌کننده انتخاب‌شده یافت نشد.");
            supplierName = supplier.PersianName;
        }

        var ingredientIds = request.Lines.Select(l => l.IngredientId).Distinct().ToList();
        var ingredientsById = await _dbContext.Ingredients
            .AsNoTracking()
            .Where(i => ingredientIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, cancellationToken);

        if (ingredientsById.Count != ingredientIds.Count)
        {
            throw new DomainException("یکی از مواد اولیه انتخاب‌شده یافت نشد.");
        }

        // Ensure main InventoryStock rows exist (StockMovement.IngredientId has FK to InventoryStocks.IngredientId).
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

        var warehouseStocksById = request.WarehouseId is { } targetWhId
            ? await _dbContext.WarehouseStocks
                .Where(s => s.WarehouseId == targetWhId && ingredientIds.Contains(s.IngredientId))
                .ToDictionaryAsync(s => s.IngredientId, cancellationToken)
            : new Dictionary<Guid, WarehouseStock>();

        // Pre-validate total requested quantity per ingredient on Goods Issue so duplicate rows
        // for the same ingredient (e.g. different batches) never bypass insufficient-stock checks.
        if (voucherType == WarehouseVoucherType.Issue)
        {
            foreach (var grp in request.Lines.GroupBy(l => l.IngredientId))
            {
                var totalQty = grp.Sum(x => x.Quantity);
                var ingredient = ingredientsById[grp.Key];
                var available = request.WarehouseId.HasValue
                    ? (warehouseStocksById.TryGetValue(grp.Key, out var ws) ? ws.Quantity : 0m)
                    : mainStocksById[grp.Key].CurrentQuantity;

                if (available < totalQty)
                {
                    throw new DomainException(
                        $"موجودی «{ingredient.PersianName}» در «{warehouseName}» برای خروج کافی نیست. موجود: {available:0.###} {ingredient.Unit} — درخواست خروج: {totalQty:0.###} {ingredient.Unit}.");
                }
            }
        }

        var actorName = await ResolveActorNameAsync(_currentUserService.UserId, cancellationToken);
        var voucherDate = request.VoucherDate ?? DateTimeOffset.UtcNow;
        var voucherNumber = await GenerateUniqueVoucherNumberAsync(voucherType, voucherDate, cancellationToken);

        var voucher = new WarehouseVoucher(
            voucherNumber,
            voucherType,
            reason,
            request.WarehouseId,
            request.SupplierId,
            request.CounterpartyName,
            request.ExternalReference,
            voucherDate,
            request.Note,
            actorName);

        var reasonLabel = WarehouseVoucher.GetReasonLabel(reason);
        var movementPrefix = voucherType == WarehouseVoucherType.Receipt
            ? $"رسید ورود {voucherNumber} | {reasonLabel}"
            : $"سند خروج {voucherNumber} | {reasonLabel}";
        var noteSuffix = string.IsNullOrWhiteSpace(request.Note) ? string.Empty : $" — {request.Note.Trim()}";

        var lineDtos = new List<WarehouseVoucherLineDto>(request.Lines.Count);

        foreach (var lineInput in request.Lines)
        {
            var ingredient = ingredientsById[lineInput.IngredientId];
            var resolvedUnitPrice = lineInput.UnitPrice ?? (ingredient.UnitCost > 0 ? ingredient.UnitCost : (decimal?)null);
            var lineMovementNote = string.IsNullOrWhiteSpace(lineInput.LineNote)
                ? $"{movementPrefix}{noteSuffix}"
                : $"{movementPrefix} — {lineInput.LineNote.Trim()}";
            if (lineMovementNote.Length > 500)
            {
                lineMovementNote = lineMovementNote[..500];
            }

            decimal stockBefore;
            decimal stockAfter;

            if (request.WarehouseId is { } whTargetId)
            {
                if (!warehouseStocksById.TryGetValue(lineInput.IngredientId, out var whStock))
                {
                    whStock = new WarehouseStock(whTargetId, lineInput.IngredientId);
                    _dbContext.WarehouseStocks.Add(whStock);
                    warehouseStocksById[lineInput.IngredientId] = whStock;
                }

                stockBefore = whStock.Quantity;
                var movement = voucherType == WarehouseVoucherType.Receipt
                    ? whStock.ReceiveVoucher(lineInput.Quantity, lineMovementNote)
                    : whStock.IssueVoucher(lineInput.Quantity, lineMovementNote);
                stockAfter = whStock.Quantity;
                _dbContext.StockMovements.Add(movement);
            }
            else
            {
                var mainStock = mainStocksById[lineInput.IngredientId];
                stockBefore = mainStock.CurrentQuantity;
                var movement = voucherType == WarehouseVoucherType.Receipt
                    ? mainStock.ReceiveVoucher(lineInput.Quantity, lineMovementNote)
                    : mainStock.IssueVoucher(lineInput.Quantity, lineMovementNote);
                stockAfter = mainStock.CurrentQuantity;
                _dbContext.StockMovements.Add(movement);
            }

            var lineEntity = voucher.AddLine(
                lineInput.IngredientId,
                lineInput.Quantity,
                resolvedUnitPrice,
                stockBefore,
                stockAfter,
                lineInput.BatchNumber,
                lineInput.ExpirationDate,
                lineInput.LineNote);

            lineDtos.Add(new WarehouseVoucherLineDto(
                lineEntity.Id,
                lineInput.IngredientId,
                ingredient.PersianName,
                ingredient.EnglishName,
                ingredient.Unit,
                lineEntity.Quantity,
                lineEntity.UnitPrice,
                lineEntity.LineTotal,
                lineEntity.StockBefore,
                lineEntity.StockAfter,
                lineEntity.BatchNumber,
                lineEntity.ExpirationDate,
                lineEntity.LineNote));
        }

        _dbContext.WarehouseVouchers.Add(voucher);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new WarehouseVoucherDto(
            voucher.Id,
            voucher.VoucherNumber,
            voucher.VoucherType.ToString(),
            voucher.VoucherType == WarehouseVoucherType.Receipt ? "رسید ورود به انبار" : "سند خروج از انبار",
            voucher.Status.ToString(),
            "ثبت قطعی",
            voucher.Reason.ToString(),
            reasonLabel,
            voucher.WarehouseId,
            warehouseName,
            voucher.SupplierId,
            supplierName,
            voucher.CounterpartyName,
            voucher.ExternalReference,
            voucher.VoucherDate,
            voucher.TotalAmount,
            lineDtos.Count,
            voucher.Note,
            voucher.CreatedByName,
            voucher.CreatedAt,
            voucher.CancelledAt,
            voucher.CancelledByName,
            voucher.CancellationReason,
            lineDtos);
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

    private async Task<string> GenerateUniqueVoucherNumberAsync(
        WarehouseVoucherType type,
        DateTimeOffset voucherDate,
        CancellationToken cancellationToken)
    {
        var prefixCode = type == WarehouseVoucherType.Receipt ? "GRN" : "GIV";
        var datePrefix = $"{prefixCode}-{voucherDate.ToUniversalTime():yyyyMMdd}-";

        var existingCount = await _dbContext.WarehouseVouchers
            .AsNoTracking()
            .CountAsync(v => v.VoucherNumber.StartsWith(datePrefix), cancellationToken);

        var seq = existingCount + 1;
        for (var attempt = 0; attempt < 25; attempt++)
        {
            var candidate = $"{datePrefix}{seq + attempt:D4}";
            var exists = await _dbContext.WarehouseVouchers
                .AsNoTracking()
                .AnyAsync(v => v.VoucherNumber == candidate, cancellationToken);
            if (!exists)
            {
                return candidate;
            }
        }

        return $"{datePrefix}{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
    }
}
