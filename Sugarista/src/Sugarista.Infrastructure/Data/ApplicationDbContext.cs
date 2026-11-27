using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();

    public DbSet<AdminUserPermission> AdminUserPermissions => Set<AdminUserPermission>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<CostControlBoard> CostControlBoards => Set<CostControlBoard>();

    public DbSet<MenuItem> MenuItems => Set<MenuItem>();

    public DbSet<MenuItemPriceHistory> MenuItemPriceHistories => Set<MenuItemPriceHistory>();

    public DbSet<MenuItemBundleItem> MenuItemBundleItems => Set<MenuItemBundleItem>();

    public DbSet<AddonItem> AddonItems => Set<AddonItem>();

    public DbSet<MenuModifier> MenuModifiers => Set<MenuModifier>();

    public DbSet<MenuItemOptionGroup> MenuItemOptionGroups => Set<MenuItemOptionGroup>();

    public DbSet<MenuItemOption> MenuItemOptions => Set<MenuItemOption>();

    public DbSet<MenuItemOptionIngredient> MenuItemOptionIngredients => Set<MenuItemOptionIngredient>();

    public DbSet<OptionGroupTemplate> OptionGroupTemplates => Set<OptionGroupTemplate>();

    public DbSet<OptionGroupTemplateItem> OptionGroupTemplateItems => Set<OptionGroupTemplateItem>();

    public DbSet<OptionGroupTemplateItemIngredient> OptionGroupTemplateItemIngredients => Set<OptionGroupTemplateItemIngredient>();

    public DbSet<Ingredient> Ingredients => Set<Ingredient>();

    public DbSet<Supplier> Suppliers => Set<Supplier>();

    public DbSet<IngredientSupplier> IngredientSuppliers => Set<IngredientSupplier>();

    public DbSet<DeliveryMethod> DeliveryMethods => Set<DeliveryMethod>();

    public DbSet<DeliveryMethodPackaging> DeliveryMethodPackagings => Set<DeliveryMethodPackaging>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<CustomerSession> CustomerSessions => Set<CustomerSession>();
    public DbSet<CustomerOtpCode> CustomerOtpCodes => Set<CustomerOtpCode>();

    public DbSet<ExportJob> ExportJobs => Set<ExportJob>();

    public DbSet<CafeOpenHour> CafeOpenHours => Set<CafeOpenHour>();

    public DbSet<AdminRole> AdminRoles => Set<AdminRole>();

    public DbSet<AdminRolePermission> AdminRolePermissions => Set<AdminRolePermission>();

    public DbSet<Employee> Employees => Set<Employee>();

    public DbSet<EmployeePayPlan> EmployeePayPlans => Set<EmployeePayPlan>();

    public DbSet<EmployeeWorkLog> EmployeeWorkLogs => Set<EmployeeWorkLog>();

    public DbSet<EmployeeShiftPlan> EmployeeShiftPlans => Set<EmployeeShiftPlan>();

    public DbSet<PunchAttendance> PunchAttendances => Set<PunchAttendance>();

    public DbSet<CafeAsset> CafeAssets => Set<CafeAsset>();

    public DbSet<CafeAssetPhoto> CafeAssetPhotos => Set<CafeAssetPhoto>();

    public DbSet<MaintenanceEquipment> MaintenanceEquipments => Set<MaintenanceEquipment>();

    public DbSet<MaintenanceSchedule> MaintenanceSchedules => Set<MaintenanceSchedule>();

    public DbSet<MaintenanceRecipient> MaintenanceRecipients => Set<MaintenanceRecipient>();

    public DbSet<MaintenanceDefaultRecipient> MaintenanceDefaultRecipients => Set<MaintenanceDefaultRecipient>();

    public DbSet<MaintenanceServiceLog> MaintenanceServiceLogs => Set<MaintenanceServiceLog>();

    public DbSet<AdminActivityLog> AdminActivityLogs => Set<AdminActivityLog>();

    public DbSet<PayrollPeriod> PayrollPeriods => Set<PayrollPeriod>();

    public DbSet<PayrollEntry> PayrollEntries => Set<PayrollEntry>();

    public DbSet<HrGlossaryTerm> HrGlossaryTerms => Set<HrGlossaryTerm>();

    public DbSet<StaffTask> StaffTasks => Set<StaffTask>();

    public DbSet<StaffTaskCompletion> StaffTaskCompletions => Set<StaffTaskCompletion>();

    public DbSet<StaffTaskActivityLog> StaffTaskActivityLogs => Set<StaffTaskActivityLog>();

    public DbSet<RecipeItem> RecipeItems => Set<RecipeItem>();

    public DbSet<InventoryStock> InventoryStocks => Set<InventoryStock>();

    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    public DbSet<InventoryPurchase> InventoryPurchases => Set<InventoryPurchase>();

    public DbSet<MenuItemStock> MenuItemStocks => Set<MenuItemStock>();

    public DbSet<MenuItemStockLot> MenuItemStockLots => Set<MenuItemStockLot>();

    public DbSet<ProductionBatch> ProductionBatches => Set<ProductionBatch>();

    public DbSet<ProductWikiGuide> ProductWikiGuides => Set<ProductWikiGuide>();

    public DbSet<ProductWikiStep> ProductWikiSteps => Set<ProductWikiStep>();

    public DbSet<BlogCategory> BlogCategories => Set<BlogCategory>();

    public DbSet<BlogPost> BlogPosts => Set<BlogPost>();

    public DbSet<BlogHighlight> BlogHighlights => Set<BlogHighlight>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderLine> OrderLines => Set<OrderLine>();

    public DbSet<OrderPayment> OrderPayments => Set<OrderPayment>();

    public DbSet<OrderSplitShare> OrderSplitShares => Set<OrderSplitShare>();

    public DbSet<SalonTable> SalonTables => Set<SalonTable>();

    public DbSet<SiteSettings> SiteSettings => Set<SiteSettings>();

    public DbSet<PrintJob> PrintJobs => Set<PrintJob>();

    public DbSet<Discount> Discounts => Set<Discount>();

    public DbSet<DiscountRedemption> DiscountRedemptions => Set<DiscountRedemption>();

    public DbSet<LoyaltyTransaction> LoyaltyTransactions => Set<LoyaltyTransaction>();

    public DbSet<StampCardReward> StampCardRewards => Set<StampCardReward>();

    public DbSet<Badge> Badges => Set<Badge>();

    public DbSet<CustomerBadge> CustomerBadges => Set<CustomerBadge>();

    public DbSet<MembershipTier> MembershipTiers => Set<MembershipTier>();

    public DbSet<CustomerWallet> CustomerWallets => Set<CustomerWallet>();

    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();

    public DbSet<WalletTopUpPackage> WalletTopUpPackages => Set<WalletTopUpPackage>();

    public DbSet<CustomerGroup> CustomerGroups => Set<CustomerGroup>();

    public DbSet<CustomerGroupMembership> CustomerGroupMemberships => Set<CustomerGroupMembership>();

    public DbSet<SubscriptionPackage> SubscriptionPackages => Set<SubscriptionPackage>();

    public DbSet<CustomerSubscription> CustomerSubscriptions => Set<CustomerSubscription>();

    public DbSet<OrderDiscount> OrderDiscounts => Set<OrderDiscount>();

    public DbSet<DiscountAttempt> DiscountAttempts => Set<DiscountAttempt>();

    public DbSet<BlacklistEntry> BlacklistEntries => Set<BlacklistEntry>();

    public DbSet<ReportSchedule> ReportSchedules => Set<ReportSchedule>();

    public DbSet<DrinkBuilderProfile> DrinkBuilderProfiles => Set<DrinkBuilderProfile>();

    public DbSet<DrinkBuilderGroup> DrinkBuilderGroups => Set<DrinkBuilderGroup>();

    public DbSet<DrinkBuilderOption> DrinkBuilderOptions => Set<DrinkBuilderOption>();

    public DbSet<AbandonedCart> AbandonedCarts => Set<AbandonedCart>();

    public DbSet<TableSession> TableSessions => Set<TableSession>();

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<Partner> Partners => Set<Partner>();

    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();

    public DbSet<JournalLine> JournalLines => Set<JournalLine>();

    public DbSet<PartnerCapitalTransaction> PartnerCapitalTransactions => Set<PartnerCapitalTransaction>();

    public DbSet<ProfitDistribution> ProfitDistributions => Set<ProfitDistribution>();

    public DbSet<ProfitDistributionLine> ProfitDistributionLines => Set<ProfitDistributionLine>();

    public DbSet<SmsProvider> SmsProviders => Set<SmsProvider>();

    public DbSet<SmsSenderLine> SmsSenderLines => Set<SmsSenderLine>();

    public DbSet<SmsMessage> SmsMessages => Set<SmsMessage>();

    public DbSet<SmsCampaign> SmsCampaigns => Set<SmsCampaign>();

    public DbSet<SmsContact> SmsContacts => Set<SmsContact>();

    public DbSet<SmsContactGroup> SmsContactGroups => Set<SmsContactGroup>();

    public DbSet<SmsContactGroupMember> SmsContactGroupMembers => Set<SmsContactGroupMember>();

    public DbSet<SmsTemplate> SmsTemplates => Set<SmsTemplate>();

    public DbSet<SmsTemplateProviderMapping> SmsTemplateProviderMappings => Set<SmsTemplateProviderMapping>();

    public DbSet<SmsOptOut> SmsOptOuts => Set<SmsOptOut>();

    public DbSet<SmsProviderLog> SmsProviderLogs => Set<SmsProviderLog>();

    public DbSet<AdminTooltip> AdminTooltips => Set<AdminTooltip>();

    public DbSet<Warehouse> Warehouses => Set<Warehouse>();

    public DbSet<WarehouseStock> WarehouseStocks => Set<WarehouseStock>();

    public DbSet<WarehouseTransfer> WarehouseTransfers => Set<WarehouseTransfer>();

    public DbSet<WarehouseTransferLine> WarehouseTransferLines => Set<WarehouseTransferLine>();

    public DbSet<WarehouseVoucher> WarehouseVouchers => Set<WarehouseVoucher>();

    public DbSet<WarehouseVoucherLine> WarehouseVoucherLines => Set<WarehouseVoucherLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
