namespace Sugarista.Application.Addons.Commands.DeleteAddonItem;

public record DeleteAddonItemCommand(Guid Id) : IRequest;

public class DeleteAddonItemCommandHandler : IRequestHandler<DeleteAddonItemCommand>
{
    private readonly IApplicationDbContext _dbContext;

    public DeleteAddonItemCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(DeleteAddonItemCommand request, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.AddonItems.FindAsync(new object[] { request.Id }, cancellationToken)
            ?? throw new KeyNotFoundException("Addon item was not found.");

        _dbContext.AddonItems.Remove(existing);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}

