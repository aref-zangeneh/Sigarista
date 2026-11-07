using MediatR;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Printing.ReceiptLayout;

namespace Sugarista.Application.Printing.Queries.GetReceiptLayout;

/// <summary>Returns the resolved receipt layout — stored JSON or the defaults.</summary>
public class GetReceiptLayoutQueryHandler : IRequestHandler<GetReceiptLayoutQuery, ResolvedReceiptLayout>
{
    private readonly IApplicationDbContext _dbContext;

    public GetReceiptLayoutQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ResolvedReceiptLayout> Handle(
        GetReceiptLayoutQuery request,
        CancellationToken cancellationToken)
    {
        var settings = await _dbContext.SiteSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        return ReceiptLayoutJsonParser.Parse(settings?.ReceiptLayoutJson);
    }
}
