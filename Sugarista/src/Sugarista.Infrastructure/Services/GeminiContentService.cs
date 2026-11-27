using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sugarista.Application.AiContent;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Infrastructure.Services;

/// <summary>
/// Stateless, draft-only Google Gemini content generator.
/// Never mutates business data and never exposes tools or actions to the model.
/// </summary>
public class GeminiContentService : IGeminiContentService
{
    private const string DefaultModel = "gemini-2.5-flash";

    private static readonly IReadOnlyList<string> SupportedModels =
    [
        "gemini-2.5-flash",
        "gemini-2.5-pro",
        "gemini-2.0-flash",
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IApplicationDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly ILogger<GeminiContentService> _logger;

    public GeminiContentService(
        IApplicationDbContext dbContext,
        IConfiguration configuration,
        HttpClient httpClient,
        ILogger<GeminiContentService> logger)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _httpClient = httpClient;
        _logger = logger;
        _httpClient.Timeout = TimeSpan.FromSeconds(45);
    }

    public async Task<GeminiSettingsStatusDto> GetSettingsStatusAsync(CancellationToken cancellationToken = default)
    {
        var (apiKey, source, model) = await ResolveCredentialsAsync(cancellationToken);
        return new GeminiSettingsStatusDto(
            IsConfigured: !string.IsNullOrWhiteSpace(apiKey),
            Source: source,
            MaskedKey: MaskKey(apiKey),
            Model: model,
            AvailableModels: SupportedModels);
    }

    public async Task<GeminiSettingsStatusDto> UpdateSettingsAsync(
        string? apiKey,
        bool clearApiKey,
        string? model,
        CancellationToken cancellationToken = default)
    {
        var settings = await _dbContext.SiteSettings.FirstOrDefaultAsync(cancellationToken);
        if (settings is null)
        {
            throw new DomainException("تنظیمات سایت یافت نشد.");
        }

        if (clearApiKey)
        {
            settings.GeminiApiKey = string.Empty;
        }
        else if (!string.IsNullOrWhiteSpace(apiKey))
        {
            var trimmed = apiKey.Trim();
            if (trimmed.Length > 256)
            {
                throw new DomainException("کلید API جمینای معتبر نیست.");
            }
            settings.GeminiApiKey = trimmed;
        }

        if (!string.IsNullOrWhiteSpace(model))
        {
            var trimmedModel = model.Trim();
            settings.GeminiModel = trimmedModel.Length > 80 ? trimmedModel[..80] : trimmedModel;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetSettingsStatusAsync(cancellationToken);
    }

    public async Task<GeminiTestResultDto> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var (apiKey, _, model) = await ResolveCredentialsAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new GeminiTestResultDto(
                Success: false,
                Message: "کلید API جمینای تنظیم نشده است. آن را در تنظیمات سایت وارد کنید یا متغیر محیطی GEMINI_API_KEY را روی سرور قرار دهید.",
                Model: model);
        }

        try
        {
            var rawText = await CallGeminiAsync(
                apiKey,
                model,
                systemPrompt: "You are a connectivity check. Reply only with JSON: {\"ok\": true}",
                userPrompt: "ping",
                temperature: 0.1,
                cancellationToken);

            var ok = !string.IsNullOrWhiteSpace(rawText);
            return new GeminiTestResultDto(
                Success: ok,
                Message: ok
                    ? $"اتصال به Gemini با موفقیت برقرار شد (مدل: {model})."
                    : "پاسخی از Gemini دریافت نشد.",
                Model: model);
        }
        catch (DomainException ex)
        {
            return new GeminiTestResultDto(Success: false, Message: ex.Message, Model: model);
        }
    }

    public async Task<GeminiDraftResultDto> GenerateDraftAsync(
        GeminiDraftRequest request,
        CancellationToken cancellationToken = default)
    {
        var section = (request.Section ?? string.Empty).Trim().ToLowerInvariant();
        var title = (request.Title ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("لطفاً ابتدا نام محصول، دسته‌بندی یا موضوع را وارد کنید تا پیش‌نویس تولید شود.");
        }

        var (apiKey, _, model) = await ResolveCredentialsAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new DomainException(
                "کلید API جمینای هنوز تنظیم نشده است. از بخش «تنظیمات سایت ← تولید محتوا با Gemini» کلید API را وارد کنید یا GEMINI_API_KEY را در سرور تنظیم نمایید.");
        }

        var cafeName = await _dbContext.SiteSettings
            .AsNoTracking()
            .Select(s => s.CafeName)
            .FirstOrDefaultAsync(cancellationToken);

        var resolvedCafe = string.IsNullOrWhiteSpace(cafeName) ? "کافه شوگاریستا" : cafeName.Trim();
        var (systemPrompt, userPrompt) = BuildPrompts(section, title, resolvedCafe, request.Context, request.Tone, request.ExtraNotes);

        var rawJsonText = await CallGeminiAsync(
            apiKey,
            model,
            systemPrompt,
            userPrompt,
            temperature: 0.7,
            cancellationToken);

        var cleaned = StripCodeFences(rawJsonText);
        try
        {
            using var doc = JsonDocument.Parse(cleaned);
            return new GeminiDraftResultDto(
                Section: section,
                Model: model,
                Draft: doc.RootElement.Clone());
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Gemini returned non-JSON payload for section {Section}", section);
            throw new DomainException("خروجی تولیدشده از Gemini ساختار معتبر نداشت. لطفاً دوباره امتحان کنید.");
        }
    }

    private async Task<(string ApiKey, string Source, string Model)> ResolveCredentialsAsync(CancellationToken cancellationToken)
    {
        var dbRow = await _dbContext.SiteSettings
            .AsNoTracking()
            .Select(s => new { s.GeminiApiKey, s.GeminiModel })
            .FirstOrDefaultAsync(cancellationToken);

        var envKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")
            ?? _configuration["Gemini:ApiKey"]
            ?? string.Empty;

        var envModel = Environment.GetEnvironmentVariable("GEMINI_MODEL")
            ?? _configuration["Gemini:Model"]
            ?? string.Empty;

        var dbKey = (dbRow?.GeminiApiKey ?? string.Empty).Trim();
        var trimmedEnvKey = envKey.Trim();

        string resolvedKey;
        string source;
        if (!string.IsNullOrWhiteSpace(dbKey))
        {
            resolvedKey = dbKey;
            source = "site_settings";
        }
        else if (!string.IsNullOrWhiteSpace(trimmedEnvKey))
        {
            resolvedKey = trimmedEnvKey;
            source = "environment";
        }
        else
        {
            resolvedKey = string.Empty;
            source = "none";
        }

        var resolvedModel = !string.IsNullOrWhiteSpace(dbRow?.GeminiModel)
            ? dbRow!.GeminiModel.Trim()
            : !string.IsNullOrWhiteSpace(envModel)
                ? envModel.Trim()
                : DefaultModel;

        return (resolvedKey, source, resolvedModel);
    }

    private async Task<string> CallGeminiAsync(
        string apiKey,
        string model,
        string systemPrompt,
        string userPrompt,
        double temperature,
        CancellationToken cancellationToken)
    {
        var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(model)}:generateContent";

        var payload = new
        {
            system_instruction = new
            {
                parts = new[] { new { text = systemPrompt } },
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = userPrompt } },
                },
            },
            generationConfig = new
            {
                temperature,
                responseMimeType = "application/json",
            },
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
        httpRequest.Headers.Add("x-goog-api-key", apiKey);
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        }
        catch (TaskCanceledException)
        {
            throw new DomainException("پاسخ‌دهی Gemini بیش از حد طول کشید. لطفاً چند لحظه بعد دوباره تلاش کنید.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Network error calling Gemini API");
            throw new DomainException("اتصال سرور به سرویس Gemini برقرار نشد. وضعیت شبکه سرور را بررسی کنید.");
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Gemini API returned {StatusCode}: {Body}", (int)response.StatusCode, body.Length > 400 ? body[..400] : body);
                throw response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                        new DomainException("کلید API جمینای نامعتبر است یا دسترسی لازم را ندارد."),
                    HttpStatusCode.TooManyRequests =>
                        new DomainException("سهمیه درخواست‌های Gemini موقتاً پر شده است. کمی بعد دوباره تلاش کنید."),
                    HttpStatusCode.NotFound =>
                        new DomainException($"مدل انتخاب‌شده ({model}) در Gemini پیدا نشد. مدل دیگری انتخاب کنید."),
                    _ => new DomainException($"خطا در ارتباط با Gemini (کد {(int)response.StatusCode})."),
                };
            }

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("candidates", out var candidates) &&
                candidates.ValueKind == JsonValueKind.Array &&
                candidates.GetArrayLength() > 0)
            {
                var first = candidates[0];
                if (first.TryGetProperty("content", out var content) &&
                    content.TryGetProperty("parts", out var parts) &&
                    parts.ValueKind == JsonValueKind.Array &&
                    parts.GetArrayLength() > 0 &&
                    parts[0].TryGetProperty("text", out var textEl))
                {
                    return textEl.GetString() ?? string.Empty;
                }
            }

            throw new DomainException("پاسخ متنی معتبری از Gemini دریافت نشد.");
        }
    }

    private static (string SystemPrompt, string UserPrompt) BuildPrompts(
        string section,
        string title,
        string cafeName,
        string? context,
        string? tone,
        string? extraNotes)
    {
        var toneLine = string.IsNullOrWhiteSpace(tone)
            ? "لحن گرم، حرفه‌ای، مدرن و اشتهابرانگیز مناسب یک کافه تخصصی (Specialty Coffee & Bakery)."
            : $"لحن موردنظر: {tone.Trim()}";

        var baseRule =
            $"شما نویسنده محتوای فارسی برای «{cafeName}» هستید. {toneLine} " +
            "همیشه و فقط یک آبجکت JSON معتبر برگردانید (بدون متن اضافه، بدون توضیح بیرون JSON). " +
            "از فارسی روان، طبیعی و بدون غلط املایی استفاده کنید.";

        var details = new StringBuilder();
        details.AppendLine($"موضوع / نام اصلی: {title}");
        if (!string.IsNullOrWhiteSpace(context))
            details.AppendLine($"زمینه و اطلاعات فعلی: {context.Trim()}");
        if (!string.IsNullOrWhiteSpace(extraNotes))
            details.AppendLine($"نکات اضافی مدیر: {extraNotes.Trim()}");

        return section switch
        {
            "blog" => (
                baseRule + """

ساختار دقیق JSON خروجی برای مقاله مجله کافه (SEO با فرمت Markdown):
{
  "title": "عنوان جذاب و سئوشده فارسی (حداکثر ۸۰ کاراکتر)",
  "slug": "english-seo-friendly-slug",
  "excerpt": "خلاصه جذاب ۲ تا ۳ جمله‌ای برای کارت مقاله",
  "contentMarkdown": "متن کامل مقاله با Markdown تمیز شامل ## سرتیترها، پاراگراف‌ها و بولت‌پوینت‌ها (۴۵۰ تا ۷۰۰ کلمه، خواندنی و کاربردی)",
  "tagsInput": "برچسب۱, برچسب۲, برچسب۳, برچسب۴",
  "metaTitle": "عنوان متا سئو (حداکثر ۶۰ کاراکتر)",
  "metaDescription": "توضیحات متا سئو (۱۲۰ تا ۱۵۵ کاراکتر)",
  "focusKeyword": "کلمه کلیدی اصلی سئو"
}
""",
                details.ToString()),

            "menu-item" => (
                baseRule + """

ساختار دقیق JSON خروجی برای آیتم منوی کافه:
{
  "englishName": "Natural English café menu name (short)",
  "description": "توضیحات اشتهابرانگیز، کوتاه و حرفه‌ای برای نمایش به مشتری در منوی دیجیتال (۲ تا ۳ جمله)",
  "tags": "برچسب۱, برچسب۲, برچسب۳",
  "badge": "یک نشان کوتاه ۱ تا ۲ کلمه‌ای مثل: پرفروش یا ویژه سرآشپز",
  "allergens": "آلرژن‌های احتمالی با کاما مثل: لبنیات، گلوتن، مغزیجات (در صورت نبود خالی بگذارید)"
}
""",
                details.ToString()),

            "category" => (
                baseRule + """

ساختار دقیق JSON خروجی برای دسته‌بندی منوی کافه:
{
  "name": "نام تمیز و استاندارد فارسی دسته‌بندی",
  "slug": "clean-lowercase-english-slug"
}
""",
                details.ToString()),

            "product-wiki" => (
                baseRule + """

ساختار دقیق JSON خروجی برای ویکی و راهنمای استاندارد آماده‌سازی باریستا/آشپزخانه:
{
  "overview": "معرفی کوتاه محصول و پروفایل طعمی آن (۲ تا ۳ جمله)",
  "qualityStandards": "استانداردهای کنترل کیفیت، بافت، دما و عطر پیش از سرو",
  "equipmentNotes": "تجهیزات موردنیاز و نکات کار با دستگاه‌ها",
  "servingNotes": "نکات سرو، نوع ظرف یا لیوان و تزیین نهایی",
  "steps": [
    { "title": "عنوان کوتاه مرحله ۱", "instructions": "توضیح دقیق و عملیاتی مرحله ۱", "durationMinutes": 1, "isQualityCheckpoint": false },
    { "title": "عنوان کوتاه مرحله ۲", "instructions": "توضیح دقیق و عملیاتی مرحله ۲", "durationMinutes": 2, "isQualityCheckpoint": true },
    { "title": "عنوان کوتاه مرحله ۳", "instructions": "توضیح دقیق و عملیاتی مرحله ۳", "durationMinutes": 1, "isQualityCheckpoint": false }
  ]
}
""",
                details.ToString()),

            "recipe" => (
                baseRule + """

ساختار دقیق JSON خروجی برای کارت دستور پخت و آماده‌سازی کافه:
{
  "recipeDescription": "توضیح کامل و شفاف روش آماده‌سازی محصول برای باریستا یا آشپزخانه",
  "bulletPoints": [
    "گام ۱: کوتاه و عملیاتی",
    "گام ۲: کوتاه و عملیاتی",
    "گام ۳: کوتاه و عملیاتی",
    "گام ۴: سرو و گارنیش نهایی"
  ],
  "servingTemp": "مثلاً داغ ۶۵ درجه یا سرد با یخ",
  "glasswareOrPlate": "مثلاً ماگ سرامیکی ۳۰۰ میل یا بشقاب دسر",
  "prepSeconds": 120,
  "baristaTip": "یک نکته طلایی برای ثبات طعم و کیفیت"
}
""",
                details.ToString()),

            _ => throw new DomainException($"بخش «{section}» برای تولید محتوا پشتیبانی نمی‌شود."),
        };
    }

    private static string StripCodeFences(string raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewLine = text.IndexOf('\n');
            if (firstNewLine >= 0)
            {
                text = text[(firstNewLine + 1)..];
            }
            if (text.EndsWith("```", StringComparison.Ordinal))
            {
                text = text[..^3];
            }
        }
        return text.Trim();
    }

    private static string MaskKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return string.Empty;
        if (key.Length <= 8) return "••••••••";
        return $"{key[..4]}••••••••{key[^4..]}";
    }
}
