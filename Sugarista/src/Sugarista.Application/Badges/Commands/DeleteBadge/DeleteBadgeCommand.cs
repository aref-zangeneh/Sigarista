using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Badges.Commands.DeleteBadge;

public record DeleteBadgeCommand(Guid Id) : IRequest;

public class DeleteBadgeCommandValidator : AbstractValidator<DeleteBadgeCommand>
{
    public DeleteBadgeCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
    }
}

public class DeleteBadgeCommandHandler : IRequestHandler<DeleteBadgeCommand>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IImageFileStore _imageStore;

    public DeleteBadgeCommandHandler(IApplicationDbContext dbContext, IImageFileStore imageStore)
    {
        _dbContext = dbContext;
        _imageStore = imageStore;
    }

    public async Task Handle(DeleteBadgeCommand request, CancellationToken cancellationToken)
    {
        var badge = await _dbContext.Badges
            .FirstOrDefaultAsync(entry => entry.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Badge was not found.");

        var imageUrl = badge.ImageUrl;
        _dbContext.Badges.Remove(badge);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (imageUrl is not null)
        {
            await _imageStore.DeleteImageAsync([imageUrl], cancellationToken);
        }
    }
}
