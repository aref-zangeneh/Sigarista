using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.DeliveryMethods.Commands.DeleteDeliveryMethod;

public record DeleteDeliveryMethodCommand(Guid Id) : IRequest;

public class DeleteDeliveryMethodCommandHandler : IRequestHandler<DeleteDeliveryMethodCommand>
{
    private readonly IApplicationDbContext _dbContext;

    public DeleteDeliveryMethodCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(DeleteDeliveryMethodCommand request, CancellationToken cancellationToken)
    {
        // Orders keep their history: FK is SetNull so orders lose method but stay intact.
        // Packaging stock movements stay attached to orders regardless.
        var method = await _dbContext.DeliveryMethods
            .FirstOrDefaultAsync(m => m.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Delivery method was not found.");

        _dbContext.DeliveryMethods.Remove(method);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
