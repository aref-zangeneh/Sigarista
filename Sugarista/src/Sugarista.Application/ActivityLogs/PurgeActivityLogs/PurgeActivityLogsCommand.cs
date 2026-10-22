using Microsoft.EntityFrameworkCore;
using Sugarista.Application.ActivityLogs.Dto;

namespace Sugarista.Application.ActivityLogs.PurgeActivityLogs;

/// <summary>Deletes log rows older than N days (retention housekeeping).</summary>
public record PurgeActivityLogsCommand(int Days) : IRequest<int>;

public class PurgeActivityLogsCommandHandler : IRequestHandler<PurgeActivityLogsCommand, int>
{
    private readonly IApplicationDbContext _dbContext;

    public PurgeActivityLogsCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int> Handle(PurgeActivityLogsCommand request, CancellationToken cancellationToken)
    {
        var days = Math.Clamp(request.Days, 1, 3650);
        var threshold = DateTimeOffset.UtcNow.AddDays(-days);

        return await _dbContext.AdminActivityLogs
            .Where(entry => entry.OccurredAt < threshold)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
