using Microsoft.EntityFrameworkCore;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Suppliers.Commands.UpsertSupplier;

public record UpsertSupplierCommand(
    Guid? Id,
    string PersianName,
    string PhoneNumber,
    string ContactName,
    string Notes,
    int DisplayOrder,
    bool IsActive = true) : IRequest<Guid>;

public class UpsertSupplierCommandValidator : AbstractValidator<UpsertSupplierCommand>
{
    public UpsertSupplierCommandValidator()
    {
        RuleFor(c => c.PersianName).NotEmpty().MaximumLength(200);
        RuleFor(c => c.PhoneNumber).MaximumLength(40);
        RuleFor(c => c.ContactName).MaximumLength(120);
        RuleFor(c => c.Notes).MaximumLength(500);
        RuleFor(c => c.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}

public class UpsertSupplierCommandHandler : IRequestHandler<UpsertSupplierCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;

    public UpsertSupplierCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Guid> Handle(UpsertSupplierCommand request, CancellationToken cancellationToken)
    {
        var persianName = request.PersianName.Trim();

        var duplicateName = await _dbContext.Suppliers
            .AsNoTracking()
            .AnyAsync(s => s.PersianName == persianName && s.Id != request.Id, cancellationToken);

        if (duplicateName)
        {
            throw new InvalidOperationException("A supplier with this name already exists.");
        }

        if (request.Id is { } supplierId)
        {
            var existing = await _dbContext.Suppliers
                .FirstOrDefaultAsync(s => s.Id == supplierId, cancellationToken)
                ?? throw new KeyNotFoundException("Supplier was not found.");

            existing.Update(
                request.PersianName,
                request.PhoneNumber,
                request.ContactName,
                request.Notes,
                request.DisplayOrder,
                request.IsActive);

            await _dbContext.SaveChangesAsync(cancellationToken);
            return existing.Id;
        }

        var supplier = new Supplier(
            request.PersianName,
            request.PhoneNumber,
            request.ContactName,
            request.Notes,
            request.DisplayOrder,
            request.IsActive);

        _dbContext.Suppliers.Add(supplier);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return supplier.Id;
    }
}
