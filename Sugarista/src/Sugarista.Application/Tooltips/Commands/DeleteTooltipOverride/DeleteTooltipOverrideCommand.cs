using MediatR;
using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;

namespace Sugarista.Application.Tooltips.Commands.DeleteTooltipOverride;

/// <summary>Deletes the override row so the registry default text is shown again.</summary>
public record DeleteTooltipOverrideCommand(string Key) : IRequest;

public class DeleteTooltipOverrideCommandHandler
    : IRequestHandler<DeleteTooltipOverrideCommand>
{
    private readonly IApplicationDbContext _dbContext;

    public DeleteTooltipOverrideCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(DeleteTooltipOverrideCommand request, CancellationToken cancellationToken)
    {
        var key = request.Key.Trim();

        var tooltip = await _dbContext.AdminTooltips
            .FirstOrDefaultAsync(entry => entry.Key == key, cancellationToken);

        if (tooltip is null)
        {
            return;
        }

        _dbContext.AdminTooltips.Remove(tooltip);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
