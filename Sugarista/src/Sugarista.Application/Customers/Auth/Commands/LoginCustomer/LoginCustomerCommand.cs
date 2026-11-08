using MediatR;
using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Customers.Services;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Customers.Auth.Commands.LoginCustomer;

/// <summary>
/// Phone-only login of the digital menu: the customer reclaims (or lazily
/// creates) their unified profile and receives a persistent session token.
/// No password and no one-time code — matching the storefront's identification
/// model — so the raw token goes to the browser cookie and only its hash is
/// persisted.
/// </summary>
public record LoginCustomerCommand(string PhoneNumber) : IRequest<CustomerLoginResultDto?>;

public record CustomerLoginResultDto(
    string Token,
    int DurationDays,
    DateTimeOffset ExpiresAtUtc,
    CustomerIdentityDto Customer);

public record CustomerIdentityDto(
    Guid CustomerId,
    string PersianName,
    string PhoneNumber);

public class LoginCustomerCommandValidator : AbstractValidator<LoginCustomerCommand>
{
    public LoginCustomerCommandValidator()
    {
        RuleFor(command => command.PhoneNumber)
            .Must(command => CustomerLoginRules.IsValidPhone(CustomerLoginRules.NormalizePhone(command)))
            .WithMessage("شماره موبایل معتبر نیست.");
    }
}

public class LoginCustomerCommandHandler : IRequestHandler<LoginCustomerCommand, CustomerLoginResultDto?>
{
    private readonly IApplicationDbContext _dbContext;

    public LoginCustomerCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CustomerLoginResultDto?> Handle(LoginCustomerCommand request, CancellationToken cancellationToken)
    {
        var phone = CustomerLoginRules.NormalizePhone(request.PhoneNumber);
        if (!CustomerLoginRules.IsValidPhone(phone))
        {
            return null;
        }

        var customer = await CustomerDirectoryService.ResolveCustomerAsync(
            _dbContext,
            customerName: null,
            phone,
            "online",
            cancellationToken);

        if (customer is null || !customer.IsActive)
        {
            return null;
        }

        customer.MarkSeen("online");

        var settings = await _dbContext.SiteSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken) ??
                       new Sugarista.Domain.Entities.SiteSettings();
        var durationDays = CustomerLoginRules.ClampDurationDays(settings.CustomerSessionDurationDays);

        var now = DateTimeOffset.UtcNow;
        var token = CustomerSession.NewToken();
        var session = new CustomerSession(customer.Id, token, now, durationDays);
        _dbContext.CustomerSessions.Add(session);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CustomerLoginResultDto(
            token,
            durationDays,
            session.ExpiresAtUtc,
            new CustomerIdentityDto(customer.Id, customer.PersianName, customer.PhoneNumber));
    }
}
