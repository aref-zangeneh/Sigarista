namespace Sugarista.Application.Carts.Services;

/// <summary>
/// Minimal, safe placeholder renderer for the admin-editable abandoned-cart
/// message templates. Placeholders look like <c>{name}</c> and are replaced
/// verbatim; the HTML variant encodes the rendered text so admin-authored
/// content can never inject markup into the email body.
/// </summary>
public static class AbandonedCartMessageTemplate
{
    public static string Render(string? template, IReadOnlyDictionary<string, string> values)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return string.Empty;
        }

        var result = template;
        foreach (var pair in values)
        {
            result = result.Replace("{" + pair.Key + "}", pair.Value, StringComparison.OrdinalIgnoreCase);
        }

        return result.Trim();
    }

    public static string RenderHtml(string? template, IReadOnlyDictionary<string, string> values)
    {
        var text = Render(template, values);
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var encoded = EscapeHtml(text);
        return encoded.Replace("\r\n", "\n").Replace("\n", "<br />");
    }

    /// <summary>
    /// Escapes only the characters that matter inside HTML while keeping
    /// Persian text readable (WebUtility.HtmlEncode would turn every non-ASCII
    /// character into a numeric entity).
    /// </summary>
    private static string EscapeHtml(string value)
        => value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
}
