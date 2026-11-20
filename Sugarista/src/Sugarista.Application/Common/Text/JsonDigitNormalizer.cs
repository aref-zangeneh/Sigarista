using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Sugarista.Application.Common.Text;

/// <summary>
/// Normalizes Persian/Arabic-Indic digits inside incoming JSON string values to ASCII digits
/// while leaving sensitive subtrees (passwords, API keys, secrets, tokens) completely untouched.
/// </summary>
public static class JsonDigitNormalizer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static bool TryNormalizeJson(string rawJson, out string normalizedJson)
    {
        normalizedJson = rawJson;
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return false;
        }

        var mayContainEscapedDigits =
            rawJson.Contains("\\u06f", StringComparison.OrdinalIgnoreCase) ||
            rawJson.Contains("\\u066", StringComparison.OrdinalIgnoreCase);

        if (!mayContainEscapedDigits && !DigitNormalizer.ContainsNonAsciiDigits(rawJson))
        {
            return false;
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(rawJson);
        }
        catch (JsonException)
        {
            return false;
        }

        if (root is null)
        {
            return false;
        }

        var changed = NormalizeNode(root);
        if (!changed)
        {
            return false;
        }

        normalizedJson = root.ToJsonString(SerializerOptions);
        return true;
    }

    private static bool NormalizeNode(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            var changed = false;
            foreach (var property in obj.ToList())
            {
                if (DigitNormalizer.IsSensitiveFieldName(property.Key) || property.Value is null)
                {
                    continue;
                }

                if (property.Value is JsonValue valueNode)
                {
                    if (TryNormalizeStringValue(valueNode, out var normalized))
                    {
                        obj[property.Key] = JsonValue.Create(normalized);
                        changed = true;
                    }
                }
                else if (NormalizeNode(property.Value))
                {
                    changed = true;
                }
            }

            return changed;
        }

        if (node is JsonArray arr)
        {
            var changed = false;
            for (var i = 0; i < arr.Count; i++)
            {
                var element = arr[i];
                if (element is null)
                {
                    continue;
                }

                if (element is JsonValue valueNode)
                {
                    if (TryNormalizeStringValue(valueNode, out var normalized))
                    {
                        arr[i] = JsonValue.Create(normalized);
                        changed = true;
                    }
                }
                else if (NormalizeNode(element))
                {
                    changed = true;
                }
            }

            return changed;
        }

        return false;
    }

    private static bool TryNormalizeStringValue(JsonValue valueNode, out string? normalized)
    {
        normalized = null;
        if (!valueNode.TryGetValue<string>(out var rawString) || rawString is null)
        {
            return false;
        }

        var converted = DigitNormalizer.ToEnglishDigits(rawString);
        if (ReferenceEquals(converted, rawString) || string.Equals(converted, rawString, StringComparison.Ordinal))
        {
            return false;
        }

        normalized = converted;
        return true;
    }
}
