namespace Sugarista.Application.Common.Extensions;

/// <summary>
/// Normalizes picture URLs before they are stored, so the admin never has to
/// type the site domain. Uploaded files come back as root-relative paths
/// (/api/files/images/...); those get the public origin prepended. Absolute
/// http(s) URLs pass through untouched.
/// </summary>
public static class ImageUrlNormalizer
{
    public static string? Normalize(string? raw, string? publicOrigin)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var url = raw.Trim();

        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        var origin = string.IsNullOrWhiteSpace(publicOrigin)
            ? null
            : publicOrigin.Trim().TrimEnd('/');

        if (origin is null)
        {
            // No configured origin: keep it root-relative — works on the
            // same origin the site is served from.
            return url.StartsWith('/') ? url : "/" + url;
        }

        return url.StartsWith('/') ? origin + url : origin + "/" + url;
    }
}
