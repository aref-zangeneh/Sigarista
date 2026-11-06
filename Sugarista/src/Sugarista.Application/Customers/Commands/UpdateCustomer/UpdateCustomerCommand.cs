using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Customers.Services;

namespace Sugarista.Application.Customers.Commands.UpdateCustomer;

public record UpdateCustomerCommand(
    Guid Id,
    string PersianName,
    string? Email,
    string? Notes,
    bool IsActive,
    string? DateOfBirth = null) : IRequest;

public class UpdateCustomerCommandValidator : AbstractValidator<UpdateCustomerCommand>
{
    public UpdateCustomerCommandValidator()
    {
        RuleFor(command => command.PersianName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(command => command.Email)
            .MaximumLength(200);

        RuleFor(command => command.DateOfBirth)
            .MaximumLength(10);
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

        // Corporate-email groups auto-approve (or revoke) as soon as the
        // customer's email is changed, without waiting for a full sweep.
        await CustomerGroupService.ApplyEmailDomainMembershipsAsync(
            _dbContext, customer, cancellationToken);

        // Birth date (feeds the birthday discount program): null = leave as is.
        if (!string.IsNullOrWhiteSpace(request.DateOfBirth))
        {
            if (DateOnly.TryParse(request.DateOfBirth, out var parsedDate))
            {
                customer.SetDateOfBirth(parsedDate);
            }
            else
            {
                throw new Sugarista.Domain.Exceptions.DomainException("تاریخ تولد معتبر نیست.");
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
