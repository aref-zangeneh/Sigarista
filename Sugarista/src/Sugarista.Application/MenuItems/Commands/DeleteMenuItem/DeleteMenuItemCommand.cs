using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.MenuItems.Commands.DeleteMenuItem;

public record DeleteMenuItemCommand(Guid Id) : IRequest;

public class DeleteMenuItemCommandHandler : IRequestHandler<DeleteMenuItemCommand>
{
    private readonly IApplicationDbContext _dbContext;

    public DeleteMenuItemCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(DeleteMenuItemCommand request, CancellationToken cancellationToken)
    {
        var item = await _dbContext.MenuItems
            .FirstOrDefaultAsync(existingItem => existingItem.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Menu item was not found.");

        _dbContext.MenuItems.Remove(item);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
