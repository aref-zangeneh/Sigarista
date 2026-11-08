using Microsoft.Extensions.Logging;
using Sugarista.Infrastructure.Data.DbConnection;

namespace Sugarista.Infrastructure.Services;

/// <summary>
/// Keeps background services from touching the database while the app is in
/// WordPress-style first-run mode (or while migrations are still running).
/// Instead of a recurring stack-trace flood ("No database provider…",
/// "relation does not exist"), each service logs one short wait message and
/// resumes automatically the moment the database is seeded and readable.
/// </summary>
public static class BackgroundDatabaseGate
{
    public static async Task WaitUntilConfiguredAsync(
        DbConnectionManager manager,
        ILogger logger,
        string serviceName,
        CancellationToken cancellationToken)
    {
        var announced = false;

        while (!manager.IsSeeded)
        {
            if (!announced)
            {
                announced = true;
                logger.LogInformation(
                    "{Service}: database not ready yet (first-run setup and migrations pending). Waiting for /api/setup…",
                    serviceName);
            }

            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
        }
    }
}
