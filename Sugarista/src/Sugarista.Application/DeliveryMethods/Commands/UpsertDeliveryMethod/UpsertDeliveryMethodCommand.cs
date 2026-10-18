using Microsoft.EntityFrameworkCore;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.DeliveryMethods.Commands.UpsertDeliveryMethod;

public record DeliveryMethodPackagingRequest(
    Guid IngredientId,
    decimal QuantityPerOrder);

public record UpsertDeliveryMethodCommand(
    Guid? Id,
    string PersianName,
    string EnglishName,
    decimal DeliveryFee,
    string? Description,
    int DisplayOrder,
    bool IsActive,
    IReadOnlyList<DeliveryMethodPackagingRequest>? Packagings) : IRequest<Guid>;

public class UpsertDeliveryMethodCommandValidator : AbstractValidator<UpsertDeliveryMethodCommand>
{
    public UpsertDeliveryMethodCommandValidator()
    {
        RuleFor(command => command.PersianName)
            .NotEmpty()
            .MaximumLength(150);

        RuleFor(command => command.EnglishName)
            .NotEmpty()
            .MaximumLength(150);

        RuleFor(command => command.DeliveryFee)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Delivery fee cannot be negative.");

        RuleFor(command => command.Description)
            .MaximumLength(1000);

        RuleFor(command => command.Packagings)
            .Must(request => (request ?? [])
                .GroupBy(line => line.IngredientId)
                .All(group => group.Count() == 1))
            .WithMessage("Each packaging item can only appear once per delivery method.");

        RuleForEach(command => command.Packagings!)
            .Must(line => line.QuantityPerOrder > 0)
            .WithMessage("Packaging quantity per order must be greater than zero.");
    }
}

public class UpsertDeliveryMethodCommandHandler : IRequestHandler<UpsertDeliveryMethodCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;

    public UpsertDeliveryMethodCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Guid> Handle(UpsertDeliveryMethodCommand request, CancellationToken cancellationToken)
    {
        var packagingRequests = (request.Packagings ?? []).ToList();

        foreach (var line in packagingRequests)
        {
            var ingredient = await _dbContext.Ingredients
                .AsNoTracking()
                .FirstOrDefaultAsync(ingredient => ingredient.Id == line.IngredientId, cancellationToken)
                ?? throw new KeyNotFoundException("Packaging item was not found.");

            if (!ingredient.IsActive)
            {
                throw new InvalidOperationException($"'{ingredient.PersianName}' is inactive and cannot be part of a delivery set.");
            }

            if (!string.Equals(ingredient.Kind, "packaging", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"'{ingredient.PersianName}' is not a packaging item. Only disposable packaging can be auto-consumed per delivery.");
            }
        }

        DeliveryMethod method;
        if (request.Id.HasValue)
        {
            method = await _dbContext.DeliveryMethods
                .Include(m => m.Packagings)
                .FirstOrDefaultAsync(m => m.Id == request.Id.Value, cancellationToken)
                ?? throw new KeyNotFoundException("Delivery method was not found.");

            var duplicate = await _dbContext.DeliveryMethods
                .AsNoTracking()
                .AnyAsync(m => m.Id != method.Id && m.PersianName == request.PersianName.Trim(), cancellationToken);
            if (duplicate)
            {
                throw new InvalidOperationException("Delivery method with the same name already exists.");
            }

            method.Update(
                request.PersianName,
                request.EnglishName,
                request.DeliveryFee,
                request.Description,
                request.DisplayOrder,
                request.IsActive);
        }
        else
        {
            var duplicate = await _dbContext.DeliveryMethods
                .AsNoTracking()
                .AnyAsync(m => m.PersianName == request.PersianName.Trim(), cancellationToken);
            if (duplicate)
            {
                throw new InvalidOperationException("Delivery method with the same name already exists.");
            }

            method = new DeliveryMethod(
                request.PersianName,
                request.EnglishName,
                request.DeliveryFee,
                request.Description,
                request.DisplayOrder,
                request.IsActive);
            _dbContext.DeliveryMethods.Add(method);
        }

        // Sync packaging lines (direct Set access to avoid tracked-collection pitfalls)
        var existingLines = request.Id.HasValue
            ? await _dbContext.DeliveryMethodPackagings.Where(l => l.DeliveryMethodId == method.Id).ToListAsync(cancellationToken)
            : [];

        foreach (var existingLine in existingLines.Where(line =>
                     !packagingRequests.Any(requested => requested.IngredientId == line.IngredientId)).ToList())
        {
            _dbContext.DeliveryMethodPackagings.Remove(existingLine);
        }

        foreach (var line in packagingRequests)
        {
            var existingLine = existingLines.FirstOrDefault(candidate => candidate.IngredientId == line.IngredientId);
            if (existingLine is not null)
            {
                if (existingLine.QuantityPerOrder != line.QuantityPerOrder)
                {
                    existingLine.UpdateQuantity(line.QuantityPerOrder);
                }
            }
            else
            {
                _dbContext.DeliveryMethodPackagings.Add(new DeliveryMethodPackaging(
                    method.Id,
                    line.IngredientId,
                    line.QuantityPerOrder));
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return method.Id;
    }
}
