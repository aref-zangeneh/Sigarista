using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.MoqAlerts.Commands.UpdateMoqThreshold;

public record UpdateMoqThresholdCommand(
    MoqSourceType SourceType,
    Guid ItemId,
    Guid? WarehouseId,
    decimal MoqQuantity) : IRequest<MoqAlertItemDto>;

public class UpdateMoqThresholdCommandValidator : AbstractValidator<UpdateMoqThresholdCommand>
{
    public UpdateMoqThresholdCommandValidator()
    {
        RuleFor(c => c.ItemId).NotEmpty();
        RuleFor(c => c.MoqQuantity)
            .GreaterThanOrEqualTo(0m)
            .LessThan(100_000_000m)
            .WithMessage("نقطه سفارش (MOQ) باید عددی غیرمنفی و معتبر باشد.");
        RuleFor(c => c.WarehouseId)
            .NotEmpty()
            .When(c => c.SourceType == MoqSourceType.WarehouseIngredient)
            .WithMessage("شناسه انبار برای موجودی انبارک الزامی است.");
    }
}

public class UpdateMoqThresholdCommandHandler : IRequestHandler<UpdateMoqThresholdCommand, MoqAlertItemDto>
{
    private readonly IApplicationDbContext _dbContext;

    public UpdateMoqThresholdCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<MoqAlertItemDto> Handle(UpdateMoqThresholdCommand request, CancellationToken cancellationToken)
    {
        switch (request.SourceType)
        {
            case MoqSourceType.Ingredient:
            {
                var ingredient = await _dbContext.Ingredients
                    .AsNoTracking()
                    .FirstOrDefaultAsync(i => i.Id == request.ItemId, cancellationToken)
                    ?? throw new KeyNotFoundException("ماده اولیه یافت نشد.");

                var stock = await _dbContext.InventoryStocks
                    .FirstOrDefaultAsync(s => s.IngredientId == request.ItemId, cancellationToken);

                if (stock is null)
                {
                    stock = new InventoryStock(request.ItemId, request.MoqQuantity);
                    _dbContext.InventoryStocks.Add(stock);
                }
                else
                {
                    stock.SetAlertThreshold(request.MoqQuantity);
                }

                await _dbContext.SaveChangesAsync(cancellationToken);

                return MoqAlertEvaluator.CreateItem(
                    MoqSourceType.Ingredient,
                    ingredient.Id,
                    null,
                    ingredient.PersianName,
                    "انبار اصلی",
                    ingredient.Unit,
                    stock.CurrentQuantity,
                    stock.MinAlertQuantity,
                    stock.LastUpdatedAt);
            }

            case MoqSourceType.MenuItem:
            {
                var menuItem = await _dbContext.MenuItems
                    .AsNoTracking()
                    .Include(m => m.Category)
                    .FirstOrDefaultAsync(m => m.Id == request.ItemId, cancellationToken)
                    ?? throw new KeyNotFoundException("آیتم منو یافت نشد.");

                var stock = await _dbContext.MenuItemStocks
                    .FirstOrDefaultAsync(s => s.MenuItemId == request.ItemId, cancellationToken);

                if (stock is null)
                {
                    stock = new MenuItemStock(request.ItemId, request.MoqQuantity);
                    _dbContext.MenuItemStocks.Add(stock);
                }
                else
                {
                    stock.SetStock(stock.CurrentQuantity, request.MoqQuantity);
                }

                await _dbContext.SaveChangesAsync(cancellationToken);

                return MoqAlertEvaluator.CreateItem(
                    MoqSourceType.MenuItem,
                    menuItem.Id,
                    null,
                    menuItem.PersianName,
                    menuItem.Category?.Name ?? "آیتم منو",
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

                stock.SetAlertThreshold(request.MoqQuantity);
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
