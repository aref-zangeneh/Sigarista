using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.Recipes.Commands.DeleteRecipeItem;

public record DeleteRecipeItemCommand(Guid Id) : IRequest;

public class DeleteRecipeItemCommandHandler : IRequestHandler<DeleteRecipeItemCommand>
{
    private readonly IApplicationDbContext _dbContext;

    public DeleteRecipeItemCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(DeleteRecipeItemCommand request, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.RecipeItems
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Recipe item was not found.");

        _dbContext.RecipeItems.Remove(existing);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
