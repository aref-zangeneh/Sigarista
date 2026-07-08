using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.Categories.Commands.DeleteCategory;

public record DeleteCategoryCommand(Guid Id) : IRequest;

public class DeleteCategoryCommandHandler : IRequestHandler<DeleteCategoryCommand>
{
    private readonly IApplicationDbContext _dbContext;

    public DeleteCategoryCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(DeleteCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _dbContext.Categories
            .Include(item => item.MenuItems)
            .FirstOrDefaultAsync(existingCategory => existingCategory.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Category was not found.");

        if (category.MenuItems.Count > 0)
        {
            throw new InvalidOperationException("Delete or reassign menu items before deleting this category.");
        }

        _dbContext.Categories.Remove(category);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
