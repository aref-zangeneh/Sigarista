using Microsoft.EntityFrameworkCore;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Categories.Commands.UpsertCategory;

public record UpsertCategoryCommand(Guid? Id, string Slug, string Name, int DisplayOrder, bool IsActive) : IRequest<Guid>;

public class UpsertCategoryCommandValidator : AbstractValidator<UpsertCategoryCommand>
{
    public UpsertCategoryCommandValidator()
    {
        RuleFor(command => command.Slug).NotEmpty().MaximumLength(100);
        RuleFor(command => command.Name).NotEmpty().MaximumLength(150);
        RuleFor(command => command.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}

public class UpsertCategoryCommandHandler : IRequestHandler<UpsertCategoryCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;

    public UpsertCategoryCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Guid> Handle(UpsertCategoryCommand request, CancellationToken cancellationToken)
    {
        var normalizedSlug = request.Slug.Trim().ToLowerInvariant();

        var duplicateExists = await _dbContext.Categories
            .AnyAsync(
                category => category.Slug == normalizedSlug && category.Id != request.Id,
                cancellationToken);

        if (duplicateExists)
        {
            throw new InvalidOperationException("A category with this slug already exists.");
        }

        if (request.Id is { } categoryId)
        {
            var existingCategory = await _dbContext.Categories
                .FirstOrDefaultAsync(category => category.Id == categoryId, cancellationToken)
                ?? throw new KeyNotFoundException("Category was not found.");

            existingCategory.Update(normalizedSlug, request.Name, request.DisplayOrder, request.IsActive);
        }
        else
        {
            _dbContext.Categories.Add(new Category(normalizedSlug, request.Name, request.DisplayOrder, request.IsActive));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return request.Id ?? await _dbContext.Categories
            .Where(category => category.Slug == normalizedSlug)
            .Select(category => category.Id)
            .FirstAsync(cancellationToken);
    }
}
