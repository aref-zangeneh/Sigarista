using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Accounting.Services;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Inventory.Commands.RecordInventoryPurchase;

public record RecordInventoryPurchaseCommand(
    Guid IngredientId,
    decimal? Quantity,
    int? PackageCount,
    decimal? PackageSize,
    DateOnly ExpirationDate,
    decimal? PurchaseUnitPrice,
    decimal ShippingFee,
    decimal TaxAmount,
    decimal IncidentalCost,
    string? Description,
    string? DocumentImageUrl,
    Guid? SupplierId = null) : IRequest<Guid>;

public class RecordInventoryPurchaseCommandValidator : AbstractValidator<RecordInventoryPurchaseCommand>
{
    public RecordInventoryPurchaseCommandValidator()
    {
        RuleFor(c => c.IngredientId).NotEmpty();

        RuleFor(c => c.SupplierId)
            .Must(id => !id.HasValue || id.Value != Guid.Empty)
            .WithMessage("Supplier id must be valid.");
        RuleFor(c => c.ExpirationDate).NotEmpty();
        RuleFor(c => c.Description).MaximumLength(1000);
        RuleFor(c => c.DocumentImageUrl).MaximumLength(2000);
        RuleFor(c => c.ShippingFee).GreaterThanOrEqualTo(0);
        RuleFor(c => c.TaxAmount).GreaterThanOrEqualTo(0);
        RuleFor(c => c.IncidentalCost).GreaterThanOrEqualTo(0);
        RuleFor(c => c.PurchaseUnitPrice).GreaterThanOrEqualTo(0).When(c => c.PurchaseUnitPrice.HasValue);

        RuleFor(c => c)
            .Must(c => ResolveQuantity(c) > 0)
            .WithMessage("Provide either total quantity or package count and package size.");

        RuleFor(c => c.PackageCount).GreaterThan(0).When(c => c.PackageCount.HasValue);
        RuleFor(c => c.PackageSize).GreaterThan(0).When(c => c.PackageSize.HasValue);
    }

    internal static decimal ResolveQuantity(RecordInventoryPurchaseCommand command)
    {
        if (command.Quantity is > 0)
        {
            return command.Quantity.Value;
        }

        if (command.PackageCount is > 0 && command.PackageSize is > 0)
        {
            return command.PackageCount.Value * command.PackageSize.Value;
        }

        return 0;
    }
}

public class RecordInventoryPurchaseCommandHandler : IRequestHandler<RecordInventoryPurchaseCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAccountingService _accountingService;

    public RecordInventoryPurchaseCommandHandler(
        IApplicationDbContext dbContext,
        IAccountingService accountingService)
    {
        _dbContext = dbContext;
        _accountingService = accountingService;
    }

    public async Task<Guid> Handle(RecordInventoryPurchaseCommand request, CancellationToken cancellationToken)
    {
        var ingredient = await _dbContext.Ingredients
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == request.IngredientId, cancellationToken)
            ?? throw new KeyNotFoundException("Ingredient was not found.");

        var quantity = RecordInventoryPurchaseCommandValidator.ResolveQuantity(request);

        var stock = await _dbContext.InventoryStocks
            .FirstOrDefaultAsync(s => s.IngredientId == request.IngredientId, cancellationToken)
            ?? throw new KeyNotFoundException("Inventory stock record was not found for this ingredient.");

        Guid? supplierId = null;
        if (request.SupplierId is { } supplier && supplier != Guid.Empty)
        {
            var supplierExists = await _dbContext.Suppliers
                .AsNoTracking()
                .AnyAsync(s => s.Id == supplier, cancellationToken);

            if (!supplierExists)
            {
                throw new KeyNotFoundException("Supplier was not found.");
            }

            supplierId = supplier;
        }

        var purchase = new InventoryPurchase(
            request.IngredientId,
            quantity,
            request.PackageCount,
            request.PackageSize,
            request.ExpirationDate,
            quantity,
            request.PurchaseUnitPrice,
            request.ShippingFee,
            request.TaxAmount,
            request.IncidentalCost,
            request.Description,
            request.DocumentImageUrl,
            supplierId);

        var note = BuildNote(purchase, ingredient.Unit);
        var movement = stock.Restock(quantity, note);

        _dbContext.InventoryPurchases.Add(purchase);
        _dbContext.StockMovements.Add(movement);

        await _dbContext.SaveChangesAsync(cancellationToken);

        purchase.LinkStockMovement(movement.Id);

        if (request.PurchaseUnitPrice is > 0)
        {
            var trackedIngredient = await _dbContext.Ingredients
                .FirstAsync(i => i.Id == request.IngredientId, cancellationToken);
            trackedIngredient.Update(
                trackedIngredient.PersianName,
                trackedIngredient.EnglishName,
                trackedIngredient.Unit,
                trackedIngredient.Description,
                trackedIngredient.DisplayOrder,
                trackedIngredient.IsActive,
                trackedIngredient.InventoryLocation,
                request.PurchaseUnitPrice.Value,
                trackedIngredient.Kind);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _accountingService.PostInventoryPurchaseAsync(purchase, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return purchase.Id;
    }

    private static string BuildNote(InventoryPurchase purchase, string unit)
    {
        var packagePart = purchase.PackageCount is > 0 && purchase.PackageSize is > 0
            ? $"{purchase.PackageCount} بسته × {purchase.PackageSize} {unit}"
            : $"{purchase.Quantity} {unit}";

        return $"خرید انبار — {packagePart} — انقضا {purchase.ExpirationDate:yyyy-MM-dd}";
    }
}
