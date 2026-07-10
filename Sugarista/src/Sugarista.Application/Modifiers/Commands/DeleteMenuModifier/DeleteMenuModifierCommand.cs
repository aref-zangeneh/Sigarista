namespace Sugarista.Application.Modifiers.Commands.DeleteMenuModifier;

public record DeleteMenuModifierCommand(Guid Id) : IRequest;

public class DeleteMenuModifierCommandHandler : IRequestHandler<DeleteMenuModifierCommand>
{
    private readonly IApplicationDbContext _dbContext;

    public DeleteMenuModifierCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(DeleteMenuModifierCommand request, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.MenuModifiers.FindAsync(new object[] { request.Id }, cancellationToken)
            ?? throw new KeyNotFoundException("Menu modifier was not found.");

        _dbContext.MenuModifiers.Remove(existing);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}

