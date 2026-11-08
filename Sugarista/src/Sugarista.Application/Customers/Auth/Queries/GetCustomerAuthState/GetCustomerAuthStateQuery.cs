using MediatR;
using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;

namespace Sugarista.Application.Customers.Auth.Queries.GetCustomerAuthState;

/// <summary>
/// Who is asking: validates the session token carried by the customer cookie
/// and returns the owning profile identity, or null when the session is
/// unknown, revoked, expired or the customer was deactivated by the café.
/// Refreshes the last-seen stamp (throttled inside the entity).
/// </summary>
public record GetCustomerAuthStateQuery(string Token) : IRequest<CustomerAuthStateDto?>;

public record CustomerAuthStateDto(
    Guid CustomerId,
    string PersianName,
    string PhoneNumber,
    DateTimeOffset ExpiresAtUtc);

public class GetCustomerAuthStateQueryHandler : IRequestHandler<GetCustomerAuthStateQuery, CustomerAuthStateDto?>
{
    private readonly IApplicationDbContext _dbContext;

    public GetCustomerAuthStateQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CustomerAuthStateDto?> Handle(GetCustomerAuthStateQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return null;
        }

        var tokenHash = Domain.Entities.CustomerSession.HashToken(request.Token);
        var now = DateTimeOffset.UtcNow;

        var session = await _dbContext.CustomerSessions
            .Include(entry => entry.Customer)
            .FirstOrDefaultAsync(
                entry => entry.TokenHash == tokenHash && entry.RevokedAtUtc == null && entry.ExpiresAtUtc > now,
                cancellationToken);

        if (session is null || !session.Customer.IsActive)
        {
            return null;
        }

        session.Touch(now);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CustomerAuthStateDto(
            session.Customer.Id,
            session.Customer.PersianName,
            session.Customer.PhoneNumber,
            session.ExpiresAtUtc);
    }
}
