using MediatR;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Printing.ReceiptLayout;

namespace Sugarista.Application.Printing.Commands.UpdateReceiptLayout;

public class UpdateReceiptLayoutCommandHandler : IRequestHandler<UpdateReceiptLayoutCommand>
{
    private readonly IApplicationDbContext _dbContext;

    public UpdateReceiptLayoutCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(UpdateReceiptLayoutCommand request, CancellationToken cancellationToken)
    {
        // Merge + validate; throws a Persian DomainException when invalid.
        var resolved = ReceiptLayoutResolver.Resolve(request.Layout);
        var storedJson = ReceiptLayoutJsonParser.Serialize(resolved);

        var settings = await _dbContext.SiteSettings
            .FirstOrDefaultAsync(cancellationToken);

        // A legacy database may not have a settings row yet — create one the
        // same way the seeder would.
        if (settings is null)
        {
            settings = new Sugarista.Domain.Entities.SiteSettings
            {
                CafeName = "Sugarista Cafe",
            };
            _dbContext.SiteSettings.Add(settings);
        }

        settings.ReceiptLayoutJson = storedJson;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
