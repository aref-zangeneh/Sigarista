using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.Discounts.Commands.DeleteDiscount;

public record DeleteDiscountCommand(Guid Id) : IRequest<Guid>;

public class DeleteDiscountCommandHandler : IRequestHandler<DeleteDiscountCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;

    public DeleteDiscountCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Guid> Handle(DeleteDiscountCommand request, CancellationToken cancellationToken)
    {
        var discount = await _dbContext.Discounts
            .FirstOrDefaultAsync(discount => discount.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Discount was not found.");

        _dbContext.Discounts.Remove(discount);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return discount.Id;
    }
}
