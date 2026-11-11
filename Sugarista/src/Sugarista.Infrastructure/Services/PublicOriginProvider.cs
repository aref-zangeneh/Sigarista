using Microsoft.Extensions.Configuration;
using Sugarista.Application.Common.Interfaces;

namespace Sugarista.Infrastructure.Services;

/// <summary>
/// The public site origin (e.g. https://sugarista.ir), read once from
/// configuration (App:PublicOrigin — fed by APP_PUBLIC_ORIGIN in compose).
/// Used to turn root-relative upload paths into absolute URLs so stored
/// picture links always carry the site domain.
/// </summary>
public class PublicOriginProvider : IPublicOriginProvider
{
    private readonly string? _origin;

    public PublicOriginProvider(IConfiguration configuration)
    {
        _origin = configuration["App:PublicOrigin"];
    }

    public string? PublicOrigin => string.IsNullOrWhiteSpace(_origin) ? null : _origin;
}
