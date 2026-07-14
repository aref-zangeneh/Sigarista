using Microsoft.EntityFrameworkCore;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.ProductWiki.Commands.UpsertProductWiki;

public record UpsertProductWikiCommand(
    Guid MenuItemId,
    string? Overview,
    string? QualityStandards,
    string? EquipmentNotes,
    string? ServingNotes,
    bool IsPublished,
    IReadOnlyCollection<ProductWikiStepInput> Steps) : IRequest<Guid>;

public record ProductWikiStepInput(
    int StepOrder,
    string Title,
    string Instructions,
    int? DurationMinutes,
    bool IsQualityCheckpoint);

public class UpsertProductWikiCommandValidator : AbstractValidator<UpsertProductWikiCommand>
{
    public UpsertProductWikiCommandValidator()
    {
        RuleFor(c => c.MenuItemId).NotEmpty();
        RuleFor(c => c.Overview).MaximumLength(4000);
        RuleFor(c => c.QualityStandards).MaximumLength(4000);
        RuleFor(c => c.EquipmentNotes).MaximumLength(2000);
        RuleFor(c => c.ServingNotes).MaximumLength(2000);
        RuleForEach(c => c.Steps).ChildRules(step =>
        {
            step.RuleFor(s => s.StepOrder).GreaterThan(0);
            step.RuleFor(s => s.Title).NotEmpty().MaximumLength(200);
            step.RuleFor(s => s.Instructions).NotEmpty().MaximumLength(4000);
            step.RuleFor(s => s.DurationMinutes).GreaterThan(0).When(s => s.DurationMinutes.HasValue);
        });
    }
}

public class UpsertProductWikiCommandHandler : IRequestHandler<UpsertProductWikiCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;

    public UpsertProductWikiCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Guid> Handle(UpsertProductWikiCommand request, CancellationToken cancellationToken)
    {
        var menuItemExists = await _dbContext.MenuItems
            .AnyAsync(m => m.Id == request.MenuItemId, cancellationToken);

        if (!menuItemExists)
        {
            throw new KeyNotFoundException("Menu item was not found.");
        }

        var guide = await _dbContext.ProductWikiGuides
            .FirstOrDefaultAsync(g => g.MenuItemId == request.MenuItemId, cancellationToken);

        if (guide is null)
        {
            guide = new ProductWikiGuide(request.MenuItemId);
            _dbContext.ProductWikiGuides.Add(guide);
        }

        guide.Update(
            request.Overview,
            request.QualityStandards,
            request.EquipmentNotes,
            request.ServingNotes,
            request.IsPublished);

        var existingSteps = await _dbContext.ProductWikiSteps
            .Where(s => s.MenuItemId == request.MenuItemId)
            .ToListAsync(cancellationToken);

        _dbContext.ProductWikiSteps.RemoveRange(existingSteps);

        foreach (var step in request.Steps.OrderBy(s => s.StepOrder))
        {
            _dbContext.ProductWikiSteps.Add(new ProductWikiStep(
                request.MenuItemId,
                step.StepOrder,
                step.Title,
                step.Instructions,
                step.DurationMinutes,
                step.IsQualityCheckpoint));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return guide.Id;
    }
}
