using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Constants;
using Sugarista.Domain.Entities;
using Sugarista.Infrastructure.Settings;

namespace Sugarista.Infrastructure.Data;

public class ApplicationDbContextSeeder
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IAdminPasswordService _adminPasswordService;
    private readonly IOptions<AdminBootstrapOptions> _adminOptions;
    private readonly ILogger<ApplicationDbContextSeeder> _logger;

    public ApplicationDbContextSeeder(
        ApplicationDbContext dbContext,
        IAdminPasswordService adminPasswordService,
        IOptions<AdminBootstrapOptions> adminOptions,
        ILogger<ApplicationDbContextSeeder> logger)
    {
        _dbContext = dbContext;
        _adminPasswordService = adminPasswordService;
        _adminOptions = adminOptions;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.Database.MigrateAsync(cancellationToken);

        await SeedSiteSettingsAsync(cancellationToken);
        await SeedMenuAsync(cancellationToken);
        await SeedAdminAsync(cancellationToken);
    }

    private async Task SeedSiteSettingsAsync(CancellationToken cancellationToken)
    {
        if (await _dbContext.SiteSettings.AnyAsync(cancellationToken))
        {
            return;
        }

        _dbContext.SiteSettings.Add(new SiteSettings
        {
            CafeName = "Sugarista Cafe",
            BrowserTitle = "Sugarista Cafe | Digital Menu",
            MetaDescription = "منوی دیجیتال کافه شوگریستا؛ مرور سریع دسته‌ها، جستجوی آسان و تجربه بهتر برای مهمان‌های QR.",
            Eyebrow = "Sugarista Cafe",
            BrandTitle = "منوی دیجیتال شوگریستا",
            BrandSubtitle = "Mobile-first QR menu for fast in-cafe browsing",
            HeroBadgeText = "Scan, choose, then order at the counter",
            HeroTitle = "منو را سریع ببینید، دسته را انتخاب کنید و راحت سفارش دهید",
            HeroDescription = "این نسخه برای UX بهتر بازطراحی شده تا مهمان بعد از اسکن QR سریع‌تر به آیتم‌ها برسد، دسته‌ها را واضح ببیند و بدون شلوغی تصمیم بگیرد.",
            HeroPrimaryButtonLabel = "شروع مرور منو",
            HeroSecondaryButtonLabel = "پرفروش‌ها",
            HeroHours = "08:00 - 23:00",
            HowToOrderLabel = "How To Order",
            HowToOrderTitle = "سفارش در 3 قدم",
            HowToOrderStatus = "Fast UX",
            StepOneTitle = "دسته را انتخاب کنید",
            StepOneDescription = "از نوار دسته‌ها برای رسیدن سریع به بخش موردنظر استفاده کنید.",
            StepTwoTitle = "در منو جستجو کنید",
            StepTwoDescription = "نام نوشیدنی یا دسر را بنویسید تا سریع پیدا شود.",
            StepThreeTitle = "به صندوق اعلام کنید",
            StepThreeDescription = "نام آیتم و تعداد را بگویید تا سفارش ثبت شود.",
            QuickAccessLabel = "Quick Access",
            QuickAccessTitle = "مسیر سریع برای انتخاب",
            QuickAccessDescription = "برای QR menu مهم است که مسیر رسیدن به آیتم کوتاه و واضح باشد.",
            PopularLabel = "Popular Now",
            PopularTitle = "پیشنهادهای سریع برای مهمان‌های جدید",
            PopularDescription = "اگر عجله دارید، از این آیتم‌های پرفروش شروع کنید.",
            MenuLabel = "Browse Menu",
            MenuTitle = "منوی کامل",
            MenuToolbarHint = "برای ثبت سفارش، نام آیتم را به صندوق اعلام کنید.",
            SearchPlaceholder = "مثلا: لاته، چیزکیک، کروسان...",
            VisitLabel = "Visit Sugarista",
            VisitTitle = "اطلاعات کافه",
            VisitDescription = "این بخش عمدا کوتاه نگه داشته شده تا تمرکز اصلی روی منو بماند و تجربه QR سریع‌تر باشد.",
            ContactPhone = "+980000000000",
            Address = "Tehran, Your Cafe Address Here",
            BreakfastHours = "08:00 تا 13:00",
            TakeawayStatus = "Available Daily",
            InstagramUrl = "https://instagram.com/",
            FooterText = "Sugarista Cafe Digital Menu Theme",
            FooterPopularLabel = "پرفروش‌ها",
            FooterMenuLabel = "منو",
            FooterCallLabel = "تماس"
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedMenuAsync(CancellationToken cancellationToken)
    {
        if (await _dbContext.Categories.AnyAsync(cancellationToken) || await _dbContext.MenuItems.AnyAsync(cancellationToken))
        {
            return;
        }

        var categories = new[]
        {
            new Category("coffee", "قهوه", 1),
            new Category("cold-drinks", "نوشیدنی سرد", 2),
            new Category("breakfast", "صبحانه", 3),
            new Category("dessert", "دسر", 4),
            new Category("tea-mocktail", "چای و موکتل", 5)
        };

        _dbContext.Categories.AddRange(categories);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var categoryMap = categories.ToDictionary(category => category.Slug, StringComparer.OrdinalIgnoreCase);

        _dbContext.MenuItems.AddRange(
            new MenuItem(categoryMap["coffee"].Id, "اسپرسو دوبل", "شات دوبل با کرمای غلیظ و طعمی متعادل؛ انتخاب کلاسیک برای شروع روز.", 98000, ["Strong", "Hot", "Classic"], "پرفروش", true, "linear-gradient(135deg, #5b3425, #8a5339)", "ES", 1),
            new MenuItem(categoryMap["coffee"].Id, "کارامل ماکیاتو", "اسپرسو، شیر بخار داده شده و لایه کارامل برای طعمی نرم و محبوب.", 145000, ["Sweet", "Milk", "Signature"], "ویژه", true, "linear-gradient(135deg, #8a4f36, #d38b52)", "CM", 2),
            new MenuItem(categoryMap["cold-drinks"].Id, "آیس لاته وانیل", "لاته خنک با رایحه وانیل، مناسب روزهای گرم و سفارش‌های بیرون‌بر.", 158000, ["Cold", "Vanilla", "Takeaway"], string.Empty, true, "linear-gradient(135deg, #7b5d4a, #c7a383)", "VL", 1),
            new MenuItem(categoryMap["cold-drinks"].Id, "موکا فراپه", "ترکیب قهوه، شکلات و یخ با بافتی خامه‌ای و طعمی دسرگونه.", 168000, ["Chocolate", "Cold", "Blended"], "جدید", false, "linear-gradient(135deg, #4f342c, #8f6655)", "MF", 2),
            new MenuItem(categoryMap["breakfast"].Id, "املت آووکادو", "املت نرم با نان تست، آووکادو و سالاد کوچک؛ مناسب صبحانه سبک و سالم.", 225000, ["Healthy", "Breakfast", "Fresh"], string.Empty, false, "linear-gradient(135deg, #507c4a, #9fc370)", "AO", 1),
            new MenuItem(categoryMap["breakfast"].Id, "پنکیک بری بری", "پنکیک نرم با میوه تازه، سس بری و کمی عسل؛ مناسب اشتراک دو نفره.", 248000, ["Sweet", "Berry", "Brunch"], "محبوب", true, "linear-gradient(135deg, #7b3754, #d9899f)", "PB", 2),
            new MenuItem(categoryMap["dessert"].Id, "چیزکیک سن سباستین", "بافت کرمی با رویه برشته و سرو کنار سس کارامل یا شکلات.", 175000, ["Dessert", "Creamy", "Best Seller"], "پرفروش", true, "linear-gradient(135deg, #a0653f, #e1b487)", "CS", 1),
            new MenuItem(categoryMap["dessert"].Id, "براونی گرم با بستنی", "براونی شکلاتی تازه با اسکوپ بستنی وانیلی و سس شکلات داغ.", 182000, ["Chocolate", "Warm", "Dessert"], string.Empty, false, "linear-gradient(135deg, #4d3025, #7f5546)", "BW", 2),
            new MenuItem(categoryMap["tea-mocktail"].Id, "موکتل رد بری", "ترکیب ردبری، لیمو و نعناع با طعمی ترش و خنک برای عصرهای تابستانی.", 152000, ["Fruity", "Iced", "Refreshing"], "Fresh", false, "linear-gradient(135deg, #8c3145, #d46b84)", "RB", 1),
            new MenuItem(categoryMap["tea-mocktail"].Id, "چای ماسالا", "چای معطر با ادویه‌های گرم و شیر، مناسب مهمان‌هایی که طعم متفاوت می‌خواهند.", 118000, ["Spicy", "Hot", "Aromatic"], string.Empty, false, "linear-gradient(135deg, #7b4937, #b97f56)", "TM", 2),
            new MenuItem(categoryMap["cold-drinks"].Id, "کلد برو پرتقال", "کلد برو روشن با عصاره پرتقال؛ سبک، میوه‌ای و مناسب دوستداران طعم خاص.", 149000, ["Cold Brew", "Citrus", "Specialty"], "باریستا چویس", false, "linear-gradient(135deg, #7b4426, #e3a155)", "CB", 3),
            new MenuItem(categoryMap["breakfast"].Id, "ساندویچ کروسان بوقلمون", "کروسان کره‌ای با بوقلمون دودی، پنیر و سس خردل ملایم.", 235000, ["Savory", "Croissant", "Popular"], string.Empty, false, "linear-gradient(135deg, #916239, #d6aa75)", "CT", 3)
        );

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedAdminAsync(CancellationToken cancellationToken)
    {
        if (await _dbContext.AdminUsers.AnyAsync(cancellationToken))
        {
            return;
        }

        var adminOptions = _adminOptions.Value;
        var adminUser = new AdminUser(
            adminOptions.Username,
            adminOptions.DisplayName,
            passwordHash: "placeholder",
            role: RoleNames.Admin);

        adminUser.SetPasswordHash(_adminPasswordService.HashPassword(adminUser, adminOptions.Password));

        _dbContext.AdminUsers.Add(adminUser);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Seeded default admin user {Username}.", adminUser.Username);
    }
}
