using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Extensions;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Categories.Commands.UpsertCategory;

public record UpsertCategoryCommand(
    Guid? Id,
    string Slug,
    string Name,
    int DisplayOrder,
    bool IsActive,
    string? ImageUrl = null) : IRequest<Guid>;

public class UpsertCategoryCommandValidator : AbstractValidator<UpsertCategoryCommand>
{
    public UpsertCategoryCommandValidator()
    {
        RuleFor(command => command.Slug).NotEmpty().MaximumLength(100);
        RuleFor(command => command.Name).NotEmpty().MaximumLength(150);
        RuleFor(command => command.DisplayOrder).GreaterThanOrEqualTo(0);
        RuleFor(command => command.ImageUrl).MaximumLength(400);
    }
}

public class UpsertCategoryCommandHandler : IRequestHandler<UpsertCategoryCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IPublicOriginProvider _publicOriginProvider;

    public UpsertCategoryCommandHandler(
        IApplicationDbContext dbContext,
        IPublicOriginProvider publicOriginProvider)
    {
        _dbContext = dbContext;
        _publicOriginProvider = publicOriginProvider;
    }

    public async Task<Guid> Handle(UpsertCategoryCommand request, CancellationToken cancellationToken)
    {
        var normalizedSlug = request.Slug.Trim().ToLowerInvariant();
        // Uploaded paths arrive root-relative; store them with the site domain
        // attached so the URL is valid wherever it is used.
        var imageUrl = ImageUrlNormalizer.Normalize(request.ImageUrl, _publicOriginProvider.PublicOrigin);

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

            existingCategory.Update(
                normalizedSlug, request.Name, request.DisplayOrder, request.IsActive, imageUrl);
        }
        else
        {
            _dbContext.Categories.Add(new Category(
                normalizedSlug, request.Name, request.DisplayOrder, request.IsActive, imageUrl));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return request.Id ?? await _dbContext.Categories
            .Where(category => category.Slug == normalizedSlug)
            .Select(category => category.Id)
            .FirstAsync(cancellationToken);
    }
}
