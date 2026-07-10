using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.Ingredients.Commands.DeleteIngredient;

public record DeleteIngredientCommand(Guid Id) : IRequest;

public class DeleteIngredientCommandHandler : IRequestHandler<DeleteIngredientCommand>
{
    private readonly IApplicationDbContext _dbContext;

    public DeleteIngredientCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(DeleteIngredientCommand request, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.Ingredients
            .FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Ingredient was not found.");

        var isUsed = await _dbContext.RecipeItems
            .AnyAsync(r => r.IngredientId == request.Id, cancellationToken);

        if (isUsed)
        {
            throw new InvalidOperationException("Cannot delete an ingredient that is used in one or more recipes. Remove it from all recipes first.");
        }

        _dbContext.Ingredients.Remove(existing);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
