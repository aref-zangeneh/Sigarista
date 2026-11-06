using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Customers.Services;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.Customers.Commands.UpdateCustomerBirthday;

/// <summary>
/// Phone-only customer self-service: the customer sets their birth date in
/// the digital menu so the birthday benefit can fire. Admins can switch this
/// off globally (locked by admin) — then this command refuses.
/// </summary>
public record UpdateCustomerBirthdayCommand(
    string PhoneNumber,
    string DateOfBirth) : IRequest;

public class UpdateCustomerBirthdayCommandValidator : AbstractValidator<UpdateCustomerBirthdayCommand>
{
    public UpdateCustomerBirthdayCommandValidator()
    {
        RuleFor(command => command.PhoneNumber).NotEmpty().MaximumLength(20);
        RuleFor(command => command.DateOfBirth).NotEmpty().MaximumLength(10);
    }
}

public class UpdateCustomerBirthdayCommandHandler : IRequestHandler<UpdateCustomerBirthdayCommand>
{
    private readonly IApplicationDbContext _dbContext;

    public UpdateCustomerBirthdayCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(UpdateCustomerBirthdayCommand request, CancellationToken cancellationToken)
    {
        var settings = await _dbContext.SiteSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        if (settings is not null && !settings.BirthdayEditableByCustomer)
        {
            throw new DomainException("تغییر تاریخ تولد در این کافه از طرف مدیریت قفل شده است.");
        }

        if (!DateOnly.TryParse(request.DateOfBirth, out var dateOfBirth))
        {
            throw new DomainException("تاریخ تولد معتبر نیست.");
        }

        var phone = CustomerDirectoryService.NormalizePhone(request.PhoneNumber);
        var customer = await _dbContext.Customers
            .FirstOrDefaultAsync(entry => entry.PhoneNumber == phone, cancellationToken);

        if (customer is null)
        {
            throw new KeyNotFoundException("Customer was not found.");
        }

        customer.SetDateOfBirth(dateOfBirth);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
