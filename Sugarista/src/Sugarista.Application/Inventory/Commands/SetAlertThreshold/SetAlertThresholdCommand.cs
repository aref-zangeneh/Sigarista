using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.Inventory.Commands.SetAlertThreshold;

public record SetAlertThresholdCommand(
    Guid IngredientId,
    decimal MinAlertQuantity) : IRequest;

public class SetAlertThresholdCommandValidator : AbstractValidator<SetAlertThresholdCommand>
{
    public SetAlertThresholdCommandValidator()
    {
        RuleFor(c => c.IngredientId).NotEmpty();
        RuleFor(c => c.MinAlertQuantity).GreaterThanOrEqualTo(0);
    }
}

public class SetAlertThresholdCommandHandler : IRequestHandler<SetAlertThresholdCommand>
{
    private readonly IApplicationDbContext _dbContext;

    public SetAlertThresholdCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(SetAlertThresholdCommand request, CancellationToken cancellationToken)
    {
        var stock = await _dbContext.InventoryStocks
            .FirstOrDefaultAsync(s => s.IngredientId == request.IngredientId, cancellationToken)
            ?? throw new KeyNotFoundException("Inventory stock record was not found for this ingredient.");

        stock.SetAlertThreshold(request.MinAlertQuantity);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
