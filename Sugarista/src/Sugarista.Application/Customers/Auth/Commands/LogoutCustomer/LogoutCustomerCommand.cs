using MediatR;
using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Customers.Auth.Commands.LogoutCustomer;

/// <summary>
/// Revokes the session carried by the customer cookie. Idempotent: an
/// unknown/expired/revoked token still exits cleanly (the cookie is deleted
/// by the API layer afterwards).
/// </summary>
public record LogoutCustomerCommand(string Token) : IRequest;

public class LogoutCustomerCommandHandler : IRequestHandler<LogoutCustomerCommand>
{
    private readonly IApplicationDbContext _dbContext;

    public LogoutCustomerCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(LogoutCustomerCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return;
        }

        var session = await _dbContext.CustomerSessions
            .FirstOrDefaultAsync(
                entry => entry.TokenHash == Domain.Entities.CustomerSession.HashToken(request.Token) &&
                         entry.RevokedAtUtc == null,
                cancellationToken);

        if (session is null)
        {
            return;
        }

        session.Revoke(DateTimeOffset.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
