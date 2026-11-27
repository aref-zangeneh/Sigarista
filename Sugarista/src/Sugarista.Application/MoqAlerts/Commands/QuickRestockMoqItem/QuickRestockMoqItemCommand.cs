using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.MoqAlerts.Commands.QuickRestockMoqItem;

public record QuickRestockMoqItemCommand(
    MoqSourceType SourceType,
    Guid ItemId,
    Guid? WarehouseId,
    decimal Quantity,
    string? Note = null) : IRequest<MoqAlertItemDto>;

public class QuickRestockMoqItemCommandValidator : AbstractValidator<QuickRestockMoqItemCommand>
{
    public QuickRestockMoqItemCommandValidator()
    {
        RuleFor(c => c.ItemId).NotEmpty();
        RuleFor(c => c.Quantity)
            .GreaterThan(0m)
            .LessThan(100_000_000m)
            .WithMessage("مقدار شارژ موجودی باید بزرگ‌تر از صفر باشد.");
        RuleFor(c => c.Note).MaximumLength(300);
        RuleFor(c => c.WarehouseId)
            .NotEmpty()
            .When(c => c.SourceType == MoqSourceType.WarehouseIngredient)
            .WithMessage("شناسه انبار الزامی است.");
    }
}

public class QuickRestockMoqItemCommandHandler : IRequestHandler<QuickRestockMoqItemCommand, MoqAlertItemDto>
{
    private readonly IApplicationDbContext _dbContext;

    public QuickRestockMoqItemCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<MoqAlertItemDto> Handle(QuickRestockMoqItemCommand request, CancellationToken cancellationToken)
    {
        var note = string.IsNullOrWhiteSpace(request.Note)
            ? "شارژ سریع از مرکز هشدار MOQ"
            : request.Note.Trim();

        switch (request.SourceType)
        {
            case MoqSourceType.Ingredient:
            {
                var stock = await _dbContext.InventoryStocks
                    .Include(s => s.Ingredient)
                    .FirstOrDefaultAsync(s => s.IngredientId == request.ItemId, cancellationToken)
                    ?? throw new KeyNotFoundException("رکورد موجودی ماده اولیه یافت نشد.");

                var movement = stock.Restock(request.Quantity, note);
                _dbContext.StockMovements.Add(movement);
                await _dbContext.SaveChangesAsync(cancellationToken);

                return MoqAlertEvaluator.CreateItem(
                    MoqSourceType.Ingredient,
                    stock.IngredientId,
                    null,
                    stock.Ingredient.PersianName,
                    "انبار اصلی",
                    stock.Ingredient.Unit,
                    stock.CurrentQuantity,
                    stock.MinAlertQuantity,
                    stock.LastUpdatedAt);
            }

            case MoqSourceType.MenuItem:
            {
                var stock = await _dbContext.MenuItemStocks
                    .Include(s => s.MenuItem)
                        .ThenInclude(m => m.Category)
                    .FirstOrDefaultAsync(s => s.MenuItemId == request.ItemId, cancellationToken)
                    ?? throw new KeyNotFoundException("رکورد موجودی آیتم منو یافت نشد.");

                stock.AddProduction(request.Quantity);
                await _dbContext.SaveChangesAsync(cancellationToken);

                return MoqAlertEvaluator.CreateItem(
                    MoqSourceType.MenuItem,
                    stock.MenuItemId,
                    null,
                    stock.MenuItem.PersianName,
                    stock.MenuItem.Category?.Name ?? "آیتم منو",
                    "عدد",
                    stock.CurrentQuantity,
                    stock.MinAlertQuantity,
                    stock.LastUpdatedAt);
            }

            case MoqSourceType.WarehouseIngredient:
            {
                var warehouseId = request.WarehouseId!.Value;
                var stock = await _dbContext.WarehouseStocks
                    .Include(s => s.Ingredient)
                    .Include(s => s.Warehouse)
                    .FirstOrDefaultAsync(
                        s => s.WarehouseId == warehouseId && s.IngredientId == request.ItemId,
                        cancellationToken)
                    ?? throw new KeyNotFoundException("رکورد موجودی انبار یافت نشد.");

                var movement = stock.Restock(request.Quantity, note);
                _dbContext.StockMovements.Add(movement);
                await _dbContext.SaveChangesAsync(cancellationToken);

                return MoqAlertEvaluator.CreateItem(
                    MoqSourceType.WarehouseIngredient,
                    stock.IngredientId,
                    stock.WarehouseId,
                    stock.Ingredient.PersianName,
                    stock.Warehouse.Name,
                    stock.Ingredient.Unit,
                    stock.Quantity,
                    stock.MinAlertQuantity,
                    stock.LastUpdatedAt);
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(request.SourceType));
        }
    }
}
