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
        await EnsureSchemaCompatibilityAsync(cancellationToken);

        await SeedSiteSettingsAsync(cancellationToken);
        await SeedMenuAsync(cancellationToken);
        await SeedSalonAsync(cancellationToken);
        await SeedAdminAsync(cancellationToken);
        await EnsureBootstrapSuperAdminAsync(cancellationToken);
        await SeedIngredientsAsync(cancellationToken);
        await SeedInventoryStocksAsync(cancellationToken);
        await SeedAccountingAccountsAsync(cancellationToken);
    }

    private async Task EnsureSchemaCompatibilityAsync(CancellationToken cancellationToken)
    {
        await _dbContext.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE "Ingredients" ADD COLUMN IF NOT EXISTS "UnitCost" numeric(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE "Ingredients" ADD COLUMN IF NOT EXISTS "Kind" character varying(20) NOT NULL DEFAULT 'ingredient';
            ALTER TABLE "InventoryPurchases" ADD COLUMN IF NOT EXISTS "SupplierId" uuid;
            CREATE TABLE IF NOT EXISTS "Suppliers" (
                "Id" uuid NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "CreatedBy" text,
                "LastModifiedAt" timestamp with time zone,
                "LastModifiedBy" text,
                "ContactName" character varying(120),
                "DisplayOrder" integer NOT NULL,
                "IsActive" boolean NOT NULL,
                "Notes" character varying(500),
                "PersianName" character varying(200) NOT NULL,
                "PhoneNumber" character varying(40),
                CONSTRAINT "PK_Suppliers" PRIMARY KEY ("Id")
            );
            CREATE TABLE IF NOT EXISTS "IngredientSuppliers" (
                "Id" uuid NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "CreatedBy" text,
                "LastModifiedAt" timestamp with time zone,
                "LastModifiedBy" text,
                "IngredientId" uuid NOT NULL,
                "IsPreferred" boolean NOT NULL DEFAULT FALSE,
                "SupplierId" uuid NOT NULL,
                CONSTRAINT "PK_IngredientSuppliers" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_IngredientSuppliers_Ingredients_IngredientId" FOREIGN KEY ("IngredientId") REFERENCES "Ingredients" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_IngredientSuppliers_Suppliers_SupplierId" FOREIGN KEY ("SupplierId") REFERENCES "Suppliers" ("Id") ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS "IX_Suppliers_DisplayOrder" ON "Suppliers" ("DisplayOrder");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Suppliers_PersianName" ON "Suppliers" ("PersianName");
            CREATE INDEX IF NOT EXISTS "IX_IngredientSuppliers_SupplierId" ON "IngredientSuppliers" ("SupplierId");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_IngredientSuppliers_IngredientId_SupplierId" ON "IngredientSuppliers" ("IngredientId", "SupplierId");
            ALTER TABLE "MenuItems" ADD COLUMN IF NOT EXISTS "RecipeDescription" character varying(4000);
            ALTER TABLE "MenuItems" ADD COLUMN IF NOT EXISTS "MarkupPercent" numeric(5,2);
            CREATE TABLE IF NOT EXISTS "DeliveryMethods" (
                "Id" uuid NOT NULL,
                "PersianName" character varying(150) NOT NULL,
                "EnglishName" character varying(150) NOT NULL,
                "Description" character varying(1000),
                "DeliveryFee" numeric(18,2) NOT NULL,
                "DisplayOrder" integer NOT NULL,
                "IsActive" boolean NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "CreatedBy" text,
                "LastModifiedAt" timestamp with time zone,
                "LastModifiedBy" text,
                CONSTRAINT "PK_DeliveryMethods" PRIMARY KEY ("Id")
            );
            CREATE TABLE IF NOT EXISTS "DeliveryMethodPackagings" (
                "Id" uuid NOT NULL,
                "DeliveryMethodId" uuid NOT NULL,
                "IngredientId" uuid NOT NULL,
                "QuantityPerOrder" numeric(12,3) NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "CreatedBy" text,
                "LastModifiedAt" timestamp with time zone,
                "LastModifiedBy" text,
                CONSTRAINT "PK_DeliveryMethodPackagings" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_DeliveryMethodPackagings_DeliveryMethods_DeliveryMethodId" FOREIGN KEY ("DeliveryMethodId") REFERENCES "DeliveryMethods" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_DeliveryMethodPackagings_Ingredients_IngredientId" FOREIGN KEY ("IngredientId") REFERENCES "Ingredients" ("Id") ON DELETE RESTRICT
            );
            CREATE INDEX IF NOT EXISTS "IX_DeliveryMethodPackagings_IngredientId" ON "DeliveryMethodPackagings" ("IngredientId");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_DeliveryMethodPackagings_DeliveryMethodId_IngredientId" ON "DeliveryMethodPackagings" ("DeliveryMethodId", "IngredientId");
            ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "DeliveryMethodId" uuid;
            ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "DeliveryFee" numeric(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "PackagingDeducted" boolean NOT NULL DEFAULT FALSE;
            ALTER TABLE "RecipeItems" ADD COLUMN IF NOT EXISTS "PreparationNote" character varying(1000);
            ALTER TABLE "SiteSettings" ADD COLUMN IF NOT EXISTS "IsOnlineSalesEnabled" boolean NOT NULL DEFAULT TRUE;
            ALTER TABLE "SiteSettings" ADD COLUMN IF NOT EXISTS "OnlineSalesNotice" character varying(500) NOT NULL DEFAULT '';
            CREATE TABLE IF NOT EXISTS "CostControlBoards" (
                "Id" uuid NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "CreatedBy" text,
                "Json" text NOT NULL,
                "LastModifiedAt" timestamp with time zone,
                "LastModifiedBy" text,
                "Version" integer NOT NULL,
                CONSTRAINT "PK_CostControlBoards" PRIMARY KEY ("Id")
            );
            CREATE TABLE IF NOT EXISTS "ProductWikiGuides" (
                "Id" uuid NOT NULL,
                "MenuItemId" uuid NOT NULL,
                "Overview" character varying(4000),
                "QualityStandards" character varying(4000),
                "EquipmentNotes" character varying(2000),
                "ServingNotes" character varying(2000),
                "IsPublished" boolean NOT NULL DEFAULT FALSE,
                "CreatedAt" timestamp with time zone NOT NULL,
                "CreatedBy" text,
                "LastModifiedAt" timestamp with time zone,
                "LastModifiedBy" text,
                CONSTRAINT "PK_ProductWikiGuides" PRIMARY KEY ("Id")
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_ProductWikiGuides_MenuItemId" ON "ProductWikiGuides" ("MenuItemId");
            CREATE TABLE IF NOT EXISTS "ProductWikiSteps" (
                "Id" uuid NOT NULL,
                "MenuItemId" uuid NOT NULL,
                "StepOrder" integer NOT NULL,
                "Title" character varying(200) NOT NULL,
                "Instructions" character varying(4000) NOT NULL,
                "DurationMinutes" integer,
                "IsQualityCheckpoint" boolean NOT NULL DEFAULT FALSE,
                "CreatedAt" timestamp with time zone NOT NULL,
                "CreatedBy" text,
                "LastModifiedAt" timestamp with time zone,
                "LastModifiedBy" text,
                CONSTRAINT "PK_ProductWikiSteps" PRIMARY KEY ("Id")
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_ProductWikiSteps_MenuItemId_StepOrder" ON "ProductWikiSteps" ("MenuItemId", "StepOrder");
            ALTER TABLE "StockMovements" ADD COLUMN IF NOT EXISTS "ProductionBatchId" uuid;
            CREATE TABLE IF NOT EXISTS "MenuItemStocks" (
                "Id" uuid NOT NULL,
                "MenuItemId" uuid NOT NULL,
                "CurrentQuantity" numeric(12,3) NOT NULL DEFAULT 0,
                "MinAlertQuantity" numeric(12,3) NOT NULL DEFAULT 0,
                "LastUpdatedAt" timestamp with time zone NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "CreatedBy" text,
                "LastModifiedAt" timestamp with time zone,
                "LastModifiedBy" text,
                CONSTRAINT "PK_MenuItemStocks" PRIMARY KEY ("Id")
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_MenuItemStocks_MenuItemId" ON "MenuItemStocks" ("MenuItemId");
            CREATE TABLE IF NOT EXISTS "ProductionBatches" (
                "Id" uuid NOT NULL,
                "MenuItemId" uuid NOT NULL,
                "QuantityProduced" numeric(12,3) NOT NULL,
                "Note" character varying(1000),
                "ProducedAt" timestamp with time zone NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "CreatedBy" text,
                "LastModifiedAt" timestamp with time zone,
                "LastModifiedBy" text,
                CONSTRAINT "PK_ProductionBatches" PRIMARY KEY ("Id")
            );
            CREATE INDEX IF NOT EXISTS "IX_ProductionBatches_MenuItemId" ON "ProductionBatches" ("MenuItemId");
            CREATE INDEX IF NOT EXISTS "IX_ProductionBatches_ProducedAt" ON "ProductionBatches" ("ProducedAt");
            CREATE INDEX IF NOT EXISTS "IX_StockMovements_ProductionBatchId" ON "StockMovements" ("ProductionBatchId");
            ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "FulfillmentStatus" character varying(30) NOT NULL DEFAULT 'None';
            ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "FulfillmentUpdatedAt" timestamp with time zone;
            ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "ReadyAt" timestamp with time zone;
            ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "ReadySmsSentAt" timestamp with time zone;
            UPDATE "Orders" SET "FulfillmentStatus" = 'Registered' WHERE "Status" = 'Paid' AND ("FulfillmentStatus" = 'None' OR "FulfillmentStatus" = '');
            CREATE INDEX IF NOT EXISTS "IX_Orders_FulfillmentStatus" ON "Orders" ("FulfillmentStatus");
            ALTER TABLE "AdminUsers" ADD COLUMN IF NOT EXISTS "IsSuperAdmin" boolean NOT NULL DEFAULT false;
            UPDATE "AdminUsers" SET "IsSuperAdmin" = true WHERE "Username" = 'admin';
            CREATE TABLE IF NOT EXISTS "AdminUserPermissions" (
                "Id" uuid NOT NULL,
                "AdminUserId" uuid NOT NULL,
                "PermissionKey" character varying(100) NOT NULL,
                CONSTRAINT "PK_AdminUserPermissions" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_AdminUserPermissions_AdminUsers_AdminUserId" FOREIGN KEY ("AdminUserId") REFERENCES "AdminUsers" ("Id") ON DELETE CASCADE
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_AdminUserPermissions_AdminUserId_PermissionKey" ON "AdminUserPermissions" ("AdminUserId", "PermissionKey");
            CREATE TABLE IF NOT EXISTS "Accounts" (
                "Id" uuid NOT NULL,
                "Code" character varying(20) NOT NULL,
                "Name" character varying(200) NOT NULL,
                "NamePersian" character varying(200) NOT NULL,
                "Type" character varying(20) NOT NULL,
                "IsSystem" boolean NOT NULL DEFAULT false,
                "IsActive" boolean NOT NULL DEFAULT true,
                "CreatedAt" timestamp with time zone NOT NULL,
                "CreatedBy" text,
                "LastModifiedAt" timestamp with time zone,
                "LastModifiedBy" text,
                CONSTRAINT "PK_Accounts" PRIMARY KEY ("Id")
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Accounts_Code" ON "Accounts" ("Code");
            CREATE TABLE IF NOT EXISTS "Partners" (
                "Id" uuid NOT NULL,
                "Name" character varying(200) NOT NULL,
                "OwnershipPercentage" numeric(8,4) NOT NULL,
                "Email" character varying(200),
                "Phone" character varying(50),
                "Notes" character varying(1000),
                "IsActive" boolean NOT NULL DEFAULT true,
                "CreatedAt" timestamp with time zone NOT NULL,
                "CreatedBy" text,
                "LastModifiedAt" timestamp with time zone,
                "LastModifiedBy" text,
                CONSTRAINT "PK_Partners" PRIMARY KEY ("Id")
            );
            CREATE TABLE IF NOT EXISTS "JournalEntries" (
                "Id" uuid NOT NULL,
                "ReferenceNumber" character varying(30) NOT NULL,
                "EntryDate" date NOT NULL,
                "SourceType" character varying(30) NOT NULL,
                "SourceId" uuid,
                "Description" character varying(1000) NOT NULL,
                "IsPosted" boolean NOT NULL DEFAULT true,
                "PostedAt" timestamp with time zone,
                "CreatedAt" timestamp with time zone NOT NULL,
                "CreatedBy" text,
                "LastModifiedAt" timestamp with time zone,
                "LastModifiedBy" text,
                CONSTRAINT "PK_JournalEntries" PRIMARY KEY ("Id")
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_JournalEntries_ReferenceNumber" ON "JournalEntries" ("ReferenceNumber");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_JournalEntries_SourceType_SourceId" ON "JournalEntries" ("SourceType", "SourceId");
            CREATE INDEX IF NOT EXISTS "IX_JournalEntries_EntryDate" ON "JournalEntries" ("EntryDate");
            CREATE TABLE IF NOT EXISTS "JournalLines" (
                "Id" uuid NOT NULL,
                "JournalEntryId" uuid NOT NULL,
                "AccountId" uuid NOT NULL,
                "Debit" numeric(18,2) NOT NULL DEFAULT 0,
                "Credit" numeric(18,2) NOT NULL DEFAULT 0,
                "Description" character varying(500),
                "PartnerId" uuid,
                CONSTRAINT "PK_JournalLines" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_JournalLines_JournalEntries_JournalEntryId" FOREIGN KEY ("JournalEntryId") REFERENCES "JournalEntries" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_JournalLines_Accounts_AccountId" FOREIGN KEY ("AccountId") REFERENCES "Accounts" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_JournalLines_Partners_PartnerId" FOREIGN KEY ("PartnerId") REFERENCES "Partners" ("Id") ON DELETE SET NULL
            );
            CREATE TABLE IF NOT EXISTS "PartnerCapitalTransactions" (
                "Id" uuid NOT NULL,
                "PartnerId" uuid NOT NULL,
                "Type" character varying(20) NOT NULL,
                "Amount" numeric(18,2) NOT NULL,
                "TransactionDate" date NOT NULL,
                "Description" character varying(1000),
                "JournalEntryId" uuid,
                "CreatedAt" timestamp with time zone NOT NULL,
                "CreatedBy" text,
                "LastModifiedAt" timestamp with time zone,
                "LastModifiedBy" text,
                CONSTRAINT "PK_PartnerCapitalTransactions" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_PartnerCapitalTransactions_Partners_PartnerId" FOREIGN KEY ("PartnerId") REFERENCES "Partners" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_PartnerCapitalTransactions_JournalEntries_JournalEntryId" FOREIGN KEY ("JournalEntryId") REFERENCES "JournalEntries" ("Id") ON DELETE SET NULL
            );
            CREATE TABLE IF NOT EXISTS "ProfitDistributions" (
                "Id" uuid NOT NULL,
                "PeriodStart" date NOT NULL,
                "PeriodEnd" date NOT NULL,
                "TotalNetProfit" numeric(18,2) NOT NULL,
                "Status" character varying(20) NOT NULL,
                "FinalizedAt" timestamp with time zone,
                "JournalEntryId" uuid,
                "CreatedAt" timestamp with time zone NOT NULL,
                "CreatedBy" text,
                "LastModifiedAt" timestamp with time zone,
                "LastModifiedBy" text,
                CONSTRAINT "PK_ProfitDistributions" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_ProfitDistributions_JournalEntries_JournalEntryId" FOREIGN KEY ("JournalEntryId") REFERENCES "JournalEntries" ("Id") ON DELETE SET NULL
            );
            CREATE TABLE IF NOT EXISTS "ProfitDistributionLines" (
                "Id" uuid NOT NULL,
                "ProfitDistributionId" uuid NOT NULL,
                "PartnerId" uuid NOT NULL,
                "OwnershipPercentage" numeric(8,4) NOT NULL,
                "AllocatedAmount" numeric(18,2) NOT NULL,
                CONSTRAINT "PK_ProfitDistributionLines" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_ProfitDistributionLines_ProfitDistributions_ProfitDistributionId" FOREIGN KEY ("ProfitDistributionId") REFERENCES "ProfitDistributions" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_ProfitDistributionLines_Partners_PartnerId" FOREIGN KEY ("PartnerId") REFERENCES "Partners" ("Id") ON DELETE RESTRICT
            );
            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            SELECT '20260722120000_AddAccountingModule', '10.0.9'
            WHERE NOT EXISTS (
                SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260722120000_AddAccountingModule'
            );
            """,
            cancellationToken);
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
            FooterCallLabel = "تماس",
            IsOnlineSalesEnabled = true,
            OnlineSalesNotice = "فروش آنلاین موقتاً غیرفعال است؛ می‌توانید حضوری سفارش دهید."
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
            role: RoleNames.SuperAdmin);

        adminUser.SetSuperAdmin(true);
        adminUser.SetPasswordHash(_adminPasswordService.HashPassword(adminUser, adminOptions.Password));

        _dbContext.AdminUsers.Add(adminUser);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Seeded default admin user {Username}.", adminUser.Username);
    }

    private async Task EnsureBootstrapSuperAdminAsync(CancellationToken cancellationToken)
    {
        var adminOptions = _adminOptions.Value;
        var bootstrapAdmin = await _dbContext.AdminUsers
            .FirstOrDefaultAsync(user => user.Username == adminOptions.Username.Trim().ToLowerInvariant(), cancellationToken);

        if (bootstrapAdmin is null || bootstrapAdmin.IsSuperAdmin)
        {
            return;
        }

        bootstrapAdmin.SetSuperAdmin(true);
        bootstrapAdmin.UpdateProfile(
            bootstrapAdmin.Username,
            bootstrapAdmin.DisplayName,
            RoleNames.SuperAdmin,
            bootstrapAdmin.IsActive,
            isSuperAdmin: true);
        bootstrapAdmin.ReplacePermissions([]);

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Promoted bootstrap admin {Username} to super admin.", bootstrapAdmin.Username);
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

    private async Task SeedAccountingAccountsAsync(CancellationToken cancellationToken)
    {
        var existingCodes = await _dbContext.Accounts
            .Select(account => account.Code)
            .ToListAsync(cancellationToken);

        foreach (var seed in AccountingAccountCodes.SeedAccounts)
        {
            if (existingCodes.Contains(seed.Code))
            {
                continue;
            }

            _dbContext.Accounts.Add(new Account(seed.Code, seed.Name, seed.NamePersian, seed.Type, isSystem: true));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
