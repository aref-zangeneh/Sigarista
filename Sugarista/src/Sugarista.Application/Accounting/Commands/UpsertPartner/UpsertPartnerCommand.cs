using Sugarista.Domain.Entities;

namespace Sugarista.Application.Accounting.Commands.UpsertPartner;

public record UpsertPartnerCommand(
    Guid? Id,
    string Name,
    decimal OwnershipPercentage,
    string? Email,
    string? Phone,
    string? Notes,
    bool IsActive = true) : IRequest<Guid>;

public class UpsertPartnerCommandValidator : AbstractValidator<UpsertPartnerCommand>
{
    public UpsertPartnerCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.OwnershipPercentage).InclusiveBetween(0, 100);
        RuleFor(command => command.Email).MaximumLength(200);
        RuleFor(command => command.Phone).MaximumLength(50);
        RuleFor(command => command.Notes).MaximumLength(1000);
    }
}

public class UpsertPartnerCommandHandler : IRequestHandler<UpsertPartnerCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;

    public UpsertPartnerCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Guid> Handle(UpsertPartnerCommand request, CancellationToken cancellationToken)
    {
        if (request.Id.HasValue)
        {
            var partner = await _dbContext.Partners
                .FirstOrDefaultAsync(entry => entry.Id == request.Id.Value, cancellationToken)
                ?? throw new KeyNotFoundException("Partner was not found.");

            partner.Update(
                request.Name,
                request.OwnershipPercentage,
                request.Email,
                request.Phone,
                request.Notes,
                request.IsActive);

            await _dbContext.SaveChangesAsync(cancellationToken);
            return partner.Id;
        }

        var totalOwnership = await _dbContext.Partners
            .Where(partner => partner.IsActive)
            .SumAsync(partner => partner.OwnershipPercentage, cancellationToken);

        if (totalOwnership + request.OwnershipPercentage > 100)
        {
            throw new InvalidOperationException("Total ownership cannot exceed 100%.");
        }

        var created = new Partner(
            request.Name,
            request.OwnershipPercentage,
            request.Email,
            request.Phone,
            request.Notes);

        if (!request.IsActive)
        {
            created.Update(request.Name, request.OwnershipPercentage, request.Email, request.Phone, request.Notes, false);
        }

        _dbContext.Partners.Add(created);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return created.Id;
    }
}
