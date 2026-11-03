using Microsoft.EntityFrameworkCore;
using MediatR;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.MembershipTiers.Commands.DeleteMembershipTier;

public record DeleteMembershipTierCommand(Guid Id) : IRequest;

public class DeleteMembershipTierCommandValidator : AbstractValidator<DeleteMembershipTierCommand>
{
    public DeleteMembershipTierCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
    }
}

public class DeleteMembershipTierCommandHandler : IRequestHandler<DeleteMembershipTierCommand>
{
    private readonly IApplicationDbContext _dbContext;

    public DeleteMembershipTierCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(DeleteMembershipTierCommand request, CancellationToken cancellationToken)
    {
        var tier = await _dbContext.MembershipTiers
            .FirstOrDefaultAsync(entry => entry.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Membership tier was not found.");

        // Customers holding the deleted level fall back to base in the next evaluation.
        var holders = await _dbContext.Customers
            .Where(customer => customer.MembershipTierId == tier.Id)
            .ToListAsync(cancellationToken);
        foreach (var holder in holders)
        {
            holder.SetMembershipTier(null);
        }

        _dbContext.MembershipTiers.Remove(tier);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
