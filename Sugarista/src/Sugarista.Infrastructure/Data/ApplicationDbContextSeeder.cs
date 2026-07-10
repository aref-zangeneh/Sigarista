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
        await SeedSalonAsync(cancellationToken);
        await SeedAdminAsync(cancellationToken);
        await SeedIngredientsAsync(cancellationToken);
        await SeedInventoryStocksAsync(cancellationToken);
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
            await SeedAddonItemsAsync(cancellationToken);
            await SeedMenuModifiersAsync(cancellationToken);
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

        MenuItem CreateMenuItem(
            string categorySlug,
            string persianName,
            string englishName,
            string description,
            int price,
            string[] tags,
            string? badge,
            bool featured,
            string accent,
            string shortLabel,
            int displayOrder)
        {
            return new MenuItem(
                categoryMap[categorySlug].Id,
                persianName,
                englishName,
                description,
                imageUrl: null,
                galleryImageUrls: [],
                specialOfferPrice: null,
                taxPercent: 0,
                isOrderable: true,
                preparingDurationMinutes: null,
                calories: null,
                allergens: [],
                volume: null,
                smallPrice: null,
                smallSpecialOfferPrice: null,
                smallVolume: null,
                mediumPrice: price,
                mediumSpecialOfferPrice: null,
                mediumVolume: null,
                largePrice: null,
                largeSpecialOfferPrice: null,
                largeVolume: null,
                tags,
                badge,
                featured,
                accent,
                shortLabel,
                displayOrder);
        }

        _dbContext.MenuItems.AddRange(
            CreateMenuItem("coffee", "اسپرسو دوبل", "Double Espresso", "شات دوبل با کرمای غلیظ و طعمی متعادل؛ انتخاب کلاسیک برای شروع روز.", 98000, ["Strong", "Hot", "Classic"], "پرفروش", true, "linear-gradient(135deg, #5b3425, #8a5339)", "ES", 1),
            CreateMenuItem("coffee", "کارامل ماکیاتو", "Caramel Macchiato", "اسپرسو، شیر بخار داده شده و لایه کارامل برای طعمی نرم و محبوب.", 145000, ["Sweet", "Milk", "Signature"], "ویژه", true, "linear-gradient(135deg, #8a4f36, #d38b52)", "CM", 2),
            CreateMenuItem("cold-drinks", "آیس لاته وانیل", "Vanilla Iced Latte", "لاته خنک با رایحه وانیل، مناسب روزهای گرم و سفارش‌های بیرون‌بر.", 158000, ["Cold", "Vanilla", "Takeaway"], string.Empty, true, "linear-gradient(135deg, #7b5d4a, #c7a383)", "VL", 1),
            CreateMenuItem("cold-drinks", "موکا فراپه", "Mocha Frappe", "ترکیب قهوه، شکلات و یخ با بافتی خامه‌ای و طعمی دسرگونه.", 168000, ["Chocolate", "Cold", "Blended"], "جدید", false, "linear-gradient(135deg, #4f342c, #8f6655)", "MF", 2),
            CreateMenuItem("breakfast", "املت آووکادو", "Avocado Omelet", "املت نرم با نان تست، آووکادو و سالاد کوچک؛ مناسب صبحانه سبک و سالم.", 225000, ["Healthy", "Breakfast", "Fresh"], string.Empty, false, "linear-gradient(135deg, #507c4a, #9fc370)", "AO", 1),
            CreateMenuItem("breakfast", "پنکیک بری بری", "Berry Pancake", "پنکیک نرم با میوه تازه، سس بری و کمی عسل؛ مناسب اشتراک دو نفره.", 248000, ["Sweet", "Berry", "Brunch"], "محبوب", true, "linear-gradient(135deg, #7b3754, #d9899f)", "PB", 2),
            CreateMenuItem("dessert", "چیزکیک سن سباستین", "San Sebastian Cheesecake", "بافت کرمی با رویه برشته و سرو کنار سس کارامل یا شکلات.", 175000, ["Dessert", "Creamy", "Best Seller"], "پرفروش", true, "linear-gradient(135deg, #a0653f, #e1b487)", "CS", 1),
            CreateMenuItem("dessert", "براونی گرم با بستنی", "Warm Brownie With Ice Cream", "براونی شکلاتی تازه با اسکوپ بستنی وانیلی و سس شکلات داغ.", 182000, ["Chocolate", "Warm", "Dessert"], string.Empty, false, "linear-gradient(135deg, #4d3025, #7f5546)", "BW", 2),
            CreateMenuItem("tea-mocktail", "موکتل رد بری", "Red Berry Mocktail", "ترکیب ردبری، لیمو و نعناع با طعمی ترش و خنک برای عصرهای تابستانی.", 152000, ["Fruity", "Iced", "Refreshing"], "Fresh", false, "linear-gradient(135deg, #8c3145, #d46b84)", "RB", 1),
            CreateMenuItem("tea-mocktail", "چای ماسالا", "Masala Tea", "چای معطر با ادویه‌های گرم و شیر، مناسب مهمان‌هایی که طعم متفاوت می‌خواهند.", 118000, ["Spicy", "Hot", "Aromatic"], string.Empty, false, "linear-gradient(135deg, #7b4937, #b97f56)", "TM", 2),
            CreateMenuItem("cold-drinks", "کلد برو پرتقال", "Orange Cold Brew", "کلد برو روشن با عصاره پرتقال؛ سبک، میوه‌ای و مناسب دوستداران طعم خاص.", 149000, ["Cold Brew", "Citrus", "Specialty"], "باریستا چویس", false, "linear-gradient(135deg, #7b4426, #e3a155)", "CB", 3),
            CreateMenuItem("breakfast", "ساندویچ کروسان بوقلمون", "Turkey Croissant Sandwich", "کروسان کره‌ای با بوقلمون دودی، پنیر و سس خردل ملایم.", 235000, ["Savory", "Croissant", "Popular"], string.Empty, false, "linear-gradient(135deg, #916239, #d6aa75)", "CT", 3)
        );

        await _dbContext.SaveChangesAsync(cancellationToken);

        await SeedAddonItemsAsync(cancellationToken);
        await SeedMenuModifiersAsync(cancellationToken);
    }

    private async Task SeedAddonItemsAsync(CancellationToken cancellationToken)
    {
        if (await _dbContext.AddonItems.AnyAsync(cancellationToken))
        {
            return;
        }

        _dbContext.AddonItems.AddRange(
            new AddonItem("شات اضافه اسپرسو", "Extra Espresso Shot", "یک شات اضافه برای نوشیدنی‌های قهوه‌ای.", 35000, ["Coffee", "Extra"], 1),
            new AddonItem("شیر بادام", "Almond Milk", "جایگزین شیر معمولی در نوشیدنی.", 30000, ["Milk", "Vegan"], 2),
            new AddonItem("شربت وانیل", "Vanilla Syrup", "افزودن شربت وانیل به نوشیدنی.", 25000, ["Syrup", "Sweet"], 3)
        );

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedMenuModifiersAsync(CancellationToken cancellationToken)
    {
        if (await _dbContext.MenuModifiers.AnyAsync(cancellationToken))
        {
            return;
        }

        _dbContext.MenuModifiers.AddRange(
            new MenuModifier("بدون شکر", "No Sugar", 1),
            new MenuModifier("کم شکر", "Less Sugar", 2),
            new MenuModifier("بدون یخ", "No Ice", 3),
            new MenuModifier("Extra Hot", "Extra Hot", 4)
        );

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedIngredientsAsync(CancellationToken cancellationToken)
    {
        if (await _dbContext.Ingredients.AnyAsync(cancellationToken))
        {
            return;
        }

        _dbContext.Ingredients.AddRange(
            new Ingredient("شیر", "Milk", "ml", "شیر تازه پاستوریزه", 1, inventoryLocation: "refrigerator"),
            new Ingredient("اسپرسو", "Espresso", "ml", "شات اسپرسو دوبل", 2, inventoryLocation: "bar"),
            new Ingredient("شکر", "Sugar", "gr", "شکر سفید معمولی", 3, inventoryLocation: "dry"),
            new Ingredient("خامه", "Cream", "ml", "خامه پرچرب قنادی", 4, inventoryLocation: "refrigerator"),
            new Ingredient("آب", "Water", "ml", "آب فیلتر شده", 5, inventoryLocation: "kitchen"),
            new Ingredient("شربت وانیل", "Vanilla Syrup", "ml", "شربت وانیل خالص", 6, inventoryLocation: "bar"),
            new Ingredient("شکلات تلخ", "Dark Chocolate", "gr", "پودر کاکائو یا شکلات ذوب شده", 7, inventoryLocation: "dry"),
            new Ingredient("پودر قهوه", "Coffee Grounds", "gr", "قهوه آسیاب شده اسپرسو", 8, inventoryLocation: "bar")
        );

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedInventoryStocksAsync(CancellationToken cancellationToken)
    {
        // Create an InventoryStock row for every ingredient that doesn't have one yet.
        var ingredientsWithoutStock = await _dbContext.Ingredients
            .Where(i => !_dbContext.InventoryStocks.Any(s => s.IngredientId == i.Id))
            .ToListAsync(cancellationToken);

        if (ingredientsWithoutStock.Count == 0)
        {
            return;
        }

        _dbContext.InventoryStocks.AddRange(
            ingredientsWithoutStock.Select(ingredient => new InventoryStock(ingredient.Id, minAlertQuantity: 0)));

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

    private async Task SeedSalonAsync(CancellationToken cancellationToken)
    {
        if (await _dbContext.SalonTables.AnyAsync(cancellationToken))
        {
            return;
        }

        _dbContext.SalonTables.AddRange(
            new SalonTable("میز ۱", 2, 1),
            new SalonTable("میز ۲", 2, 2),
            new SalonTable("میز ۳", 4, 3),
            new SalonTable("میز ۴", 4, 4),
            new SalonTable("میز ۵", 6, 5),
            new SalonTable("میز VIP", 8, 6));

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
