namespace Sugarista.Application.Common.Interfaces;

/// <summary>
/// The public site origin (e.g. https://sugarista.ir), or null when the
/// deployment does not configure one. Implementations read it from
/// configuration once at startup.
/// </summary>
public interface IPublicOriginProvider
{
    string? PublicOrigin { get; }
}
