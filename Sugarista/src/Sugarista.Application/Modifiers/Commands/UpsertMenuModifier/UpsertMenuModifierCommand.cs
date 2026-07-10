using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.Modifiers.Commands.UpsertMenuModifier;

public record UpsertMenuModifierCommand(
    Guid? Id,
    string PersianName,
    string EnglishName,
    int DisplayOrder,
    bool IsActive) : IRequest<Guid>;

public class UpsertMenuModifierCommandValidator : AbstractValidator<UpsertMenuModifierCommand>
{
    public UpsertMenuModifierCommandValidator()
    {
        RuleFor(command => command.PersianName).NotEmpty().MaximumLength(200);
        RuleFor(command => command.EnglishName).NotEmpty().MaximumLength(200);
        RuleFor(command => command.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}

public class UpsertMenuModifierCommandHandler : IRequestHandler<UpsertMenuModifierCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;

    public UpsertMenuModifierCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Guid> Handle(UpsertMenuModifierCommand request, CancellationToken cancellationToken)
    {
        if (request.Id is { } modifierId)
        {
            var existing = await _dbContext.MenuModifiers
                .FirstOrDefaultAsync(modifier => modifier.Id == modifierId, cancellationToken)
                ?? throw new KeyNotFoundException("Menu modifier was not found.");

            existing.Update(request.PersianName, request.EnglishName, request.DisplayOrder, request.IsActive);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return existing.Id;
        }

        var modifier = new Sugarista.Domain.Entities.MenuModifier(
            request.PersianName,
            request.EnglishName,
            request.DisplayOrder,
            request.IsActive);

        _dbContext.MenuModifiers.Add(modifier);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return modifier.Id;
    }
}

