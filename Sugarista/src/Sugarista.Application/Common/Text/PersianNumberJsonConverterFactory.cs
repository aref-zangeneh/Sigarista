using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sugarista.Application.Common.Text;

/// <summary>
/// Allows numeric DTO properties (int, long, short, decimal, double, float and nullables)
/// to accept either JSON numbers or string representations containing Persian/Arabic-Indic digits.
/// Always serializes back as standard JSON numbers.
/// </summary>
public sealed class PersianNumberJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        var coreType = Nullable.GetUnderlyingType(typeToConvert) ?? typeToConvert;
        return coreType == typeof(int)
            || coreType == typeof(long)
            || coreType == typeof(short)
            || coreType == typeof(decimal)
            || coreType == typeof(double)
            || coreType == typeof(float);
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var underlying = Nullable.GetUnderlyingType(typeToConvert);
        if (underlying is not null)
        {
            return CreateNullableConverter(underlying);
        }

        return CreateNonNullableConverter(typeToConvert);
    }

    private static JsonConverter CreateNonNullableConverter(Type type)
    {
        if (type == typeof(int)) return new NumericJsonConverter<int>();
        if (type == typeof(long)) return new NumericJsonConverter<long>();
        if (type == typeof(short)) return new NumericJsonConverter<short>();
        if (type == typeof(decimal)) return new NumericJsonConverter<decimal>();
        if (type == typeof(double)) return new NumericJsonConverter<double>();
        if (type == typeof(float)) return new NumericJsonConverter<float>();
        throw new NotSupportedException($"Unsupported numeric type: {type.FullName}");
    }

    private static JsonConverter CreateNullableConverter(Type underlying)
    {
        if (underlying == typeof(int)) return new NullableNumericJsonConverter<int>();
        if (underlying == typeof(long)) return new NullableNumericJsonConverter<long>();
        if (underlying == typeof(short)) return new NullableNumericJsonConverter<short>();
        if (underlying == typeof(decimal)) return new NullableNumericJsonConverter<decimal>();
        if (underlying == typeof(double)) return new NullableNumericJsonConverter<double>();
        if (underlying == typeof(float)) return new NullableNumericJsonConverter<float>();
        throw new NotSupportedException($"Unsupported nullable numeric type: {underlying.FullName}");
    }

    private static T ParseNormalized<T>(string? raw, Type targetType) where T : struct, INumber<T>
    {
        var normalized = DigitNormalizer.NormalizeNumericToken(raw);
        if (normalized.Length == 0 ||
            !T.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new JsonException($"Invalid numeric value '{raw}' for type {targetType.Name}.");
        }

        return parsed;
    }

    private static T ReadToken<T>(ref Utf8JsonReader reader) where T : struct, INumber<T>
    {
        if (typeof(T) == typeof(int)) return (T)(object)reader.GetInt32();
        if (typeof(T) == typeof(long)) return (T)(object)reader.GetInt64();
        if (typeof(T) == typeof(short)) return (T)(object)reader.GetInt16();
        if (typeof(T) == typeof(decimal)) return (T)(object)reader.GetDecimal();
        if (typeof(T) == typeof(double)) return (T)(object)reader.GetDouble();
        if (typeof(T) == typeof(float)) return (T)(object)reader.GetSingle();
        throw new NotSupportedException($"Unsupported numeric type: {typeof(T).FullName}");
    }

    private static void WriteToken<T>(Utf8JsonWriter writer, T value) where T : struct, INumber<T>
    {
        switch (value)
        {
            case int i: writer.WriteNumberValue(i); break;
            case long l: writer.WriteNumberValue(l); break;
            case short s: writer.WriteNumberValue(s); break;
            case decimal d: writer.WriteNumberValue(d); break;
            case double dbl: writer.WriteNumberValue(dbl); break;
            case float f: writer.WriteNumberValue(f); break;
            default: throw new NotSupportedException($"Unsupported numeric type: {typeof(T).FullName}");
        }
    }

    private sealed class NumericJsonConverter<T> : JsonConverter<T> where T : struct, INumber<T>
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.TokenType switch
            {
                JsonTokenType.Number => ReadToken<T>(ref reader),
                JsonTokenType.String => ParseNormalized<T>(reader.GetString(), typeToConvert),
                _ => throw new JsonException($"Cannot convert token {reader.TokenType} to {typeToConvert.Name}."),
            };
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
            WriteToken(writer, value);
    }

    private sealed class NullableNumericJsonConverter<T> : JsonConverter<T?> where T : struct, INumber<T>
    {
        public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType == JsonTokenType.Number)
            {
                return ReadToken<T>(ref reader);
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                var raw = reader.GetString();
                if (string.IsNullOrWhiteSpace(raw))
                {
                    return null;
                }

                return ParseNormalized<T>(raw, typeof(T));
            }

            throw new JsonException($"Cannot convert token {reader.TokenType} to Nullable<{typeof(T).Name}>.");
        }

        public override void Write(Utf8JsonWriter writer, T? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
            {
                WriteToken(writer, value.Value);
            }
            else
            {
                writer.WriteNullValue();
            }
        }
    }
}
