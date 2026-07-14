using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;

namespace Sugarista.Application.Orders;

internal static class OrderTrackingCodeGenerator
{
    internal static async Task<string> GenerateAsync(IApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var code = $"SG-{Random.Shared.Next(1000, 9999)}";
            var exists = await dbContext.Orders.AnyAsync(o => o.TrackingCode == code, cancellationToken);
            if (!exists)
            {
                return code;
            }
        }

        return $"SG-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
    }
}
