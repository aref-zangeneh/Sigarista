using System.Text.Json;
using Sugarista.Application.Common.Text;

namespace Sugarista.Application.UnitTests.Common;

public class DigitNormalizerTests
{
    private sealed record SampleNumericPayload(
        int Count,
        int? OptionalCount,
        long BigCount,
        decimal Price,
        decimal? DiscountPrice,
        double Ratio);

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new PersianNumberJsonConverterFactory());
        return options;
    }

    [Fact]
    public void ToEnglishDigits_ConvertsPersianAndArabicIndic_AndKeepsAsciiInstance()
    {
        const string ascii = "09123456789 - ABC";
        var untouched = DigitNormalizer.ToEnglishDigits(ascii);
        Assert.Same(ascii, untouched);

        Assert.Null(DigitNormalizer.ToEnglishDigits(null));
        Assert.Equal(string.Empty, DigitNormalizer.ToEnglishDigits(string.Empty));

        Assert.Equal("0123456789", DigitNormalizer.ToEnglishDigits("۰۱۲۳۴۵۶۷۸۹"));
        Assert.Equal("0123456789", DigitNormalizer.ToEnglishDigits("٠١٢٣٤٥٦٧٨٩"));
        Assert.Equal("قهوه 100% عربیکا - 08:30 تا 23:45", DigitNormalizer.ToEnglishDigits("قهوه ۱۰۰% عربیکا - ۰۸:۳۰ تا ۲۳:۴۵"));
    }

    [Fact]
    public void JsonDigitNormalizer_NormalizesRegularStringsAndArrays_WhilePreservingSecrets()
    {
        const string input = """
            {
              "phone": "۰۹۱۲۳۴۵۶۷۸۹",
              "hours": "۰۸:۰۰ تا ۲۳:۳۰",
              "title": "بسته \u06f1\u06f0 عددی",
              "tags": ["شماره ۱", "مدل ٠٢"],
              "nested": {
                "shamsiDate": "۱۴۰۵/۰۸/۲۹",
                "secret": "p@ss۰۱۲",
                "deliveryWebhookSecret": "sec-۹۹"
              },
              "password": "MyPassword۱۲۳",
              "currentPassword": "OldPassword۴۵۶",
              "newPassword": "NewPassword۷۸۹",
              "adminPassword": "AdminPass۰۹۸",
              "apiKey": "key-۱۲۳۴",
              "extraSettingsJson": "{\"pin\":\"۱۲۳۴\"}",
              "token": "tok-۵۶۷"
            }
            """;

        var changed = JsonDigitNormalizer.TryNormalizeJson(input, out var normalized);
        Assert.True(changed);

        using var doc = JsonDocument.Parse(normalized);
        var root = doc.RootElement;
        Assert.Equal("09123456789", root.GetProperty("phone").GetString());
        Assert.Equal("08:00 تا 23:30", root.GetProperty("hours").GetString());
        Assert.Equal("بسته 10 عددی", root.GetProperty("title").GetString());
        Assert.Equal("شماره 1", root.GetProperty("tags")[0].GetString());
        Assert.Equal("مدل 02", root.GetProperty("tags")[1].GetString());
        Assert.Equal("1405/08/29", root.GetProperty("nested").GetProperty("shamsiDate").GetString());

        // Sensitive fields must remain 100% untouched.
        Assert.Equal("p@ss۰۱۲", root.GetProperty("nested").GetProperty("secret").GetString());
        Assert.Equal("sec-۹۹", root.GetProperty("nested").GetProperty("deliveryWebhookSecret").GetString());
        Assert.Equal("MyPassword۱۲۳", root.GetProperty("password").GetString());
        Assert.Equal("OldPassword۴۵۶", root.GetProperty("currentPassword").GetString());
        Assert.Equal("NewPassword۷۸۹", root.GetProperty("newPassword").GetString());
        Assert.Equal("AdminPass۰۹۸", root.GetProperty("adminPassword").GetString());
        Assert.Equal("key-۱۲۳۴", root.GetProperty("apiKey").GetString());
        Assert.Equal("{\"pin\":\"۱۲۳۴\"}", root.GetProperty("extraSettingsJson").GetString());
        Assert.Equal("tok-۵۶۷", root.GetProperty("token").GetString());
    }

    [Fact]
    public void JsonDigitNormalizer_ReturnsFalseOnPlainAsciiOrMalformedJson()
    {
        Assert.False(JsonDigitNormalizer.TryNormalizeJson("""{"count":12,"name":"Latte 100%"}""", out _));
        Assert.False(JsonDigitNormalizer.TryNormalizeJson("{invalid-json ۱۱۲", out _));
    }

    [Fact]
    public void PersianNumberJsonConverterFactory_DeserializesPersianStringsAndNumbers_AndWritesNumbers()
    {
        const string json = """
            {
              "count": "۴۲",
              "optionalCount": "",
              "bigCount": "۱۲٬۳۴۵٬۶۷۸",
              "price": "۱۲۵٬۰۰۰",
              "discountPrice": "۱۲٫۵",
              "ratio": "٠٫۷۵"
            }
            """;

        var parsed = JsonSerializer.Deserialize<SampleNumericPayload>(json, JsonOptions);
        Assert.NotNull(parsed);
        Assert.Equal(42, parsed!.Count);
        Assert.Null(parsed.OptionalCount);
        Assert.Equal(12_345_678L, parsed.BigCount);
        Assert.Equal(125_000m, parsed.Price);
        Assert.Equal(12.5m, parsed.DiscountPrice);
        Assert.Equal(0.75d, parsed.Ratio, 4);

        const string persianCommaThousandsJson = """
            {
              "count": "۱۰",
              "optionalCount": null,
              "bigCount": "۱۲۵،۰۰۰",
              "price": "۱۲۵،۰۰۰٫۵",
              "discountPrice": null,
              "ratio": "۱٫۵"
            }
            """;

        var withPersianComma = JsonSerializer.Deserialize<SampleNumericPayload>(persianCommaThousandsJson, JsonOptions);
        Assert.NotNull(withPersianComma);
        Assert.Equal(125_000L, withPersianComma!.BigCount);
        Assert.Equal(125_000.5m, withPersianComma.Price);

        const string standardJson = """
            {
              "count": 7,
              "optionalCount": null,
              "bigCount": 9999999999,
              "price": 150000.5,
              "discountPrice": 5000,
              "ratio": 0.25
            }
            """;

        var fromNumbers = JsonSerializer.Deserialize<SampleNumericPayload>(standardJson, JsonOptions);
        Assert.NotNull(fromNumbers);
        Assert.Equal(7, fromNumbers!.Count);
        Assert.Null(fromNumbers.OptionalCount);
        Assert.Equal(150000.5m, fromNumbers.Price);

        var serialized = JsonSerializer.Serialize(fromNumbers, JsonOptions);
        using var doc = JsonDocument.Parse(serialized);
        Assert.Equal(JsonValueKind.Number, doc.RootElement.GetProperty("count").ValueKind);
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("optionalCount").ValueKind);
        Assert.Equal(JsonValueKind.Number, doc.RootElement.GetProperty("price").ValueKind);
    }

    [Fact]
    public void PersianNumberJsonConverterFactory_RejectsInvalidStringForNonNullableInt()
    {
        const string badJson = """
            {
              "count": "abc",
              "optionalCount": null,
              "bigCount": 1,
              "price": 10,
              "discountPrice": null,
              "ratio": 1
            }
            """;

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SampleNumericPayload>(badJson, JsonOptions));
    }
}
