using Microsoft.EntityFrameworkCore;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.OptionTemplates.Commands.DeleteOptionGroupTemplate;

/// <summary>
/// Safely removes a template from the library. Any <see cref="Domain.Entities.MenuItemOptionGroup"/>
/// previously created from this template stays intact on its menu item with SourceTemplateId set to null.
/// </summary>
public record DeleteOptionGroupTemplateCommand(Guid Id) : IRequest;

public class DeleteOptionGroupTemplateCommandHandler : IRequestHandler<DeleteOptionGroupTemplateCommand>
{
    private readonly IApplicationDbContext _dbContext;

    public DeleteOptionGroupTemplateCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(DeleteOptionGroupTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _dbContext.OptionGroupTemplates
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new DomainException("قالب گروه آپشن یافت نشد.");

        _dbContext.OptionGroupTemplates.Remove(template);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
