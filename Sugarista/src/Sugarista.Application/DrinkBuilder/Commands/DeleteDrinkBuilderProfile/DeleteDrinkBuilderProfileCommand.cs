using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.DrinkBuilder.Commands.DeleteDrinkBuilderProfile;

public record DeleteDrinkBuilderProfileCommand(Guid Id) : IRequest;

public class DeleteDrinkBuilderProfileCommandHandler : IRequestHandler<DeleteDrinkBuilderProfileCommand>
{
    private readonly IApplicationDbContext _dbContext;

    public DeleteDrinkBuilderProfileCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(DeleteDrinkBuilderProfileCommand request, CancellationToken cancellationToken)
    {
        var profile = await _dbContext.DrinkBuilderProfiles
            .Include(entry => entry.Groups)
            .ThenInclude(group => group.Options)
            .FirstOrDefaultAsync(entry => entry.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Drink builder profile was not found.");

        _dbContext.DrinkBuilderProfiles.Remove(profile);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
