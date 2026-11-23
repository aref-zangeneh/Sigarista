using System.Text.Json;
using System.Text.Json.Serialization;
using Sugarista.Application.Printing.ReceiptLayout;

namespace Sugarista.Application.Printing;

/// <summary>
/// (De)serializes the resolved receipt layout document against the exact
/// camelCase shape the offline print agent understands.
/// </summary>
public static class ReceiptLayoutJsonParser
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Stored JSON → resolved document. Anything invalid falls back to the defaults.</summary>
    public static ResolvedReceiptLayout Parse(string? storedJson)
    {
        if (string.IsNullOrWhiteSpace(storedJson))
        {
            return ReceiptLayoutDefault.All();
        }

        try
        {
            // Deserialize as a partial patch and resolve against built-in defaults
            // so legacy stored JSON documents automatically inherit newly added
            // customization fields without losing any saved merchant preferences.
            var patch = JsonSerializer.Deserialize<ReceiptLayoutPatch>(storedJson, SerializerOptions);
            if (patch?.Customer is not null || patch?.Barista is not null)
            {
                return ReceiptLayoutResolver.Resolve(patch);
            }
        }
        catch (Exception)
        {
            // Fall through to the defaults below.
        }

        return ReceiptLayoutDefault.All();
    }

    /// <summary>Resolved document (or a section of it) → JSON stored on SiteSettings.</summary>
    public static string Serialize(object document)
        => JsonSerializer.Serialize(document, SerializerOptions);
}
