using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Maintenance.Queries.GetMaintenanceEquipments;

namespace Sugarista.Application.Maintenance.Queries.GetMaintenanceDefaultRecipients;

/// <summary>The global fallback reminder targets (site admin's SMS/email list).</summary>
public record GetMaintenanceDefaultRecipientsQuery : IRequest<IReadOnlyList<MaintenanceRecipientDto>>;

public class GetMaintenanceDefaultRecipientsQueryHandler
    : IRequestHandler<GetMaintenanceDefaultRecipientsQuery, IReadOnlyList<MaintenanceRecipientDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetMaintenanceDefaultRecipientsQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<MaintenanceRecipientDto>> Handle(
        GetMaintenanceDefaultRecipientsQuery request,
        CancellationToken cancellationToken)
    {
        return await _dbContext.MaintenanceDefaultRecipients
            .AsNoTracking()
            .OrderBy(recipient => recipient.Channel)
            .ThenBy(recipient => recipient.Target)
            .Select(recipient => new MaintenanceRecipientDto(
                recipient.Id,
                recipient.Channel.ToString(),
                recipient.Target,
                recipient.Label))
            .ToListAsync(cancellationToken);
    }
}
