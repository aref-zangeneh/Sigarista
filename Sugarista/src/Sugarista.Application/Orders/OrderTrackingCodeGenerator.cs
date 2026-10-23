using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;

namespace Sugarista.Application.Orders;

internal static class OrderTrackingCodeGenerator
{
    // 32 unambiguous characters (no 0/O/1/I) give ~1.07 billion six-character
    // combinations, making the public tracking-code lookup impractical to brute force.
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int CodeLength = 6;

    internal static async Task<string> GenerateAsync(IApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var code = $"SG-{RandomCode()}";
            var exists = await dbContext.Orders.AnyAsync(o => o.TrackingCode == code, cancellationToken);
            if (!exists)
            {
                return code;
            }
        }

        return $"SG-{Guid.NewGuid():N}"[..11].ToUpperInvariant();
    }

    private static string RandomCode()
    {
        var characters = new char[CodeLength];

        for (var index = 0; index < characters.Length; index++)
        {
            characters[index] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return new string(characters);
    }
}
