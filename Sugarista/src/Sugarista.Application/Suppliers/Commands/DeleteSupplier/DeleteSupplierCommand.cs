namespace Sugarista.Application.Suppliers.Commands.DeleteSupplier;

public record DeleteSupplierCommand(Guid Id) : IRequest;

public class DeleteSupplierCommandHandler : IRequestHandler<DeleteSupplierCommand>
{
    private readonly IApplicationDbContext _dbContext;

    public DeleteSupplierCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(DeleteSupplierCommand request, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.Suppliers
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Supplier was not found.");

        // Ingredient links cascade away; recorded purchases keep history (supplier becomes NULL).
        _dbContext.Suppliers.Remove(existing);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
