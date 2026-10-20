using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.Customers.Commands.UpdateCustomer;

public record UpdateCustomerCommand(
    Guid Id,
    string PersianName,
    string? Email,
    string? Notes,
    bool IsActive) : IRequest;

public class UpdateCustomerCommandValidator : AbstractValidator<UpdateCustomerCommand>
{
    public UpdateCustomerCommandValidator()
    {
        RuleFor(command => command.PersianName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(command => command.Email)
            .MaximumLength(200);
    }
}

public class UpdateCustomerCommandHandler : IRequestHandler<UpdateCustomerCommand>
{
    private readonly IApplicationDbContext _dbContext;

    public UpdateCustomerCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(UpdateCustomerCommand request, CancellationToken cancellationToken)
    {
        var customer = await _dbContext.Customers
            .FirstOrDefaultAsync(entry => entry.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Customer was not found.");

        customer.Update(request.PersianName, request.Email, request.Notes, request.IsActive);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
