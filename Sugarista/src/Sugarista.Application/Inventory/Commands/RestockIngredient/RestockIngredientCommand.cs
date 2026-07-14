using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.Inventory.Commands.RestockIngredient;

public record RestockIngredientCommand(
    Guid IngredientId,
    decimal Quantity,
    string Note) : IRequest;

public class RestockIngredientCommandValidator : AbstractValidator<RestockIngredientCommand>
{
    public RestockIngredientCommandValidator()
    {
        RuleFor(c => c.IngredientId).NotEmpty();
        RuleFor(c => c.Quantity).GreaterThan(0).WithMessage("Restock quantity must be greater than zero.");
        RuleFor(c => c.Note).MaximumLength(500);
    }
}

public class RestockIngredientCommandHandler : IRequestHandler<RestockIngredientCommand>
{
    private readonly IApplicationDbContext _dbContext;

    public RestockIngredientCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(RestockIngredientCommand request, CancellationToken cancellationToken)
    {
        var stock = await _dbContext.InventoryStocks
            .FirstOrDefaultAsync(s => s.IngredientId == request.IngredientId, cancellationToken)
            ?? throw new KeyNotFoundException("Inventory stock record was not found for this ingredient.");

        var movement = stock.Restock(request.Quantity, request.Note);
        _dbContext.StockMovements.Add(movement);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
