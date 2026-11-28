namespace Sugarista.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Sugarista.Domain.Entities.AdminUser> AdminUsers { get; }

    DbSet<Sugarista.Domain.Entities.AdminUserPermission> AdminUserPermissions { get; }

    DbSet<Sugarista.Domain.Entities.Category> Categories { get; }

    DbSet<Sugarista.Domain.Entities.CostControlBoard> CostControlBoards { get; }

    DbSet<Sugarista.Domain.Entities.MenuItem> MenuItems { get; }

    DbSet<Sugarista.Domain.Entities.MenuItemPriceHistory> MenuItemPriceHistories { get; }

    DbSet<Sugarista.Domain.Entities.MenuItemBundleItem> MenuItemBundleItems { get; }

    DbSet<Sugarista.Domain.Entities.AddonItem> AddonItems { get; }

    DbSet<Sugarista.Domain.Entities.MenuModifier> MenuModifiers { get; }

    DbSet<Sugarista.Domain.Entities.MenuItemOptionGroup> MenuItemOptionGroups { get; }

    DbSet<Sugarista.Domain.Entities.MenuItemOption> MenuItemOptions { get; }

    DbSet<Sugarista.Domain.Entities.MenuItemOptionIngredient> MenuItemOptionIngredients { get; }

    DbSet<Sugarista.Domain.Entities.OptionGroupTemplate> OptionGroupTemplates { get; }

    DbSet<Sugarista.Domain.Entities.OptionGroupTemplateItem> OptionGroupTemplateItems { get; }

    DbSet<Sugarista.Domain.Entities.OptionGroupTemplateItemIngredient> OptionGroupTemplateItemIngredients { get; }

    DbSet<Sugarista.Domain.Entities.Ingredient> Ingredients { get; }

    DbSet<Sugarista.Domain.Entities.Supplier> Suppliers { get; }

    DbSet<Sugarista.Domain.Entities.DeliveryMethod> DeliveryMethods { get; }

    DbSet<Sugarista.Domain.Entities.Customer> Customers { get; }

    DbSet<Sugarista.Domain.Entities.CustomerSession> CustomerSessions { get; }
    DbSet<Sugarista.Domain.Entities.CustomerOtpCode> CustomerOtpCodes { get; }

    DbSet<Sugarista.Domain.Entities.ExportJob> ExportJobs { get; }

    DbSet<Sugarista.Domain.Entities.AdminRole> AdminRoles { get; }

    DbSet<Sugarista.Domain.Entities.AdminRolePermission> AdminRolePermissions { get; }

    DbSet<Sugarista.Domain.Entities.CafeOpenHour> CafeOpenHours { get; }

    DbSet<Sugarista.Domain.Entities.Employee> Employees { get; }

    DbSet<Sugarista.Domain.Entities.EmployeePayPlan> EmployeePayPlans { get; }

    DbSet<Sugarista.Domain.Entities.EmployeeWorkLog> EmployeeWorkLogs { get; }

    DbSet<Sugarista.Domain.Entities.EmployeeShiftPlan> EmployeeShiftPlans { get; }

    DbSet<Sugarista.Domain.Entities.PunchAttendance> PunchAttendances { get; }

    DbSet<Sugarista.Domain.Entities.CafeAsset> CafeAssets { get; }

    DbSet<Sugarista.Domain.Entities.CafeAssetPhoto> CafeAssetPhotos { get; }

    DbSet<Sugarista.Domain.Entities.MaintenanceEquipment> MaintenanceEquipments { get; }

    DbSet<Sugarista.Domain.Entities.MaintenanceSchedule> MaintenanceSchedules { get; }

    DbSet<Sugarista.Domain.Entities.MaintenanceRecipient> MaintenanceRecipients { get; }

    DbSet<Sugarista.Domain.Entities.MaintenanceDefaultRecipient> MaintenanceDefaultRecipients { get; }

    DbSet<Sugarista.Domain.Entities.MaintenanceServiceLog> MaintenanceServiceLogs { get; }

    DbSet<Sugarista.Domain.Entities.AdminActivityLog> AdminActivityLogs { get; }

    DbSet<Sugarista.Domain.Entities.PayrollPeriod> PayrollPeriods { get; }

    DbSet<Sugarista.Domain.Entities.PayrollEntry> PayrollEntries { get; }

    DbSet<Sugarista.Domain.Entities.HrGlossaryTerm> HrGlossaryTerms { get; }

    DbSet<Sugarista.Domain.Entities.StaffTask> StaffTasks { get; }

    DbSet<Sugarista.Domain.Entities.StaffTaskCompletion> StaffTaskCompletions { get; }

    DbSet<Sugarista.Domain.Entities.StaffTaskActivityLog> StaffTaskActivityLogs { get; }

    DbSet<Sugarista.Domain.Entities.DeliveryMethodPackaging> DeliveryMethodPackagings { get; }

    DbSet<Sugarista.Domain.Entities.IngredientSupplier> IngredientSuppliers { get; }

    DbSet<Sugarista.Domain.Entities.RecipeItem> RecipeItems { get; }

    DbSet<Sugarista.Domain.Entities.MenuItemRecipeCard> MenuItemRecipeCards { get; }

    DbSet<Sugarista.Domain.Entities.InventoryStock> InventoryStocks { get; }

    DbSet<Sugarista.Domain.Entities.StockMovement> StockMovements { get; }

    DbSet<Sugarista.Domain.Entities.InventoryPurchase> InventoryPurchases { get; }

    DbSet<Sugarista.Domain.Entities.MenuItemStock> MenuItemStocks { get; }

    DbSet<Sugarista.Domain.Entities.MenuItemStockLot> MenuItemStockLots { get; }

    DbSet<Sugarista.Domain.Entities.SupplierOrderListItem> SupplierOrderListItems { get; }

    DbSet<Sugarista.Domain.Entities.AdminBankCard> AdminBankCards { get; }

    DbSet<Sugarista.Domain.Entities.CafeIncident> CafeIncidents { get; }

    DbSet<Sugarista.Domain.Entities.CustomerDemand> CustomerDemands { get; }

    DbSet<Sugarista.Domain.Entities.BackupCloudTarget> BackupCloudTargets { get; }

    DbSet<Sugarista.Domain.Entities.BackupJob> BackupJobs { get; }

    DbSet<Sugarista.Domain.Entities.BackupRecord> BackupRecords { get; }

    DbSet<Sugarista.Domain.Entities.ProductionBatch> ProductionBatches { get; }

    DbSet<Sugarista.Domain.Entities.ProductWikiGuide> ProductWikiGuides { get; }

    DbSet<Sugarista.Domain.Entities.ProductWikiStep> ProductWikiSteps { get; }

    DbSet<Sugarista.Domain.Entities.BlogCategory> BlogCategories { get; }

    DbSet<Sugarista.Domain.Entities.BlogPost> BlogPosts { get; }

    DbSet<Sugarista.Domain.Entities.BlogHighlight> BlogHighlights { get; }

    DbSet<Sugarista.Domain.Entities.Order> Orders { get; }

    DbSet<Sugarista.Domain.Entities.OrderLine> OrderLines { get; }

    DbSet<Sugarista.Domain.Entities.OrderPayment> OrderPayments { get; }

    DbSet<Sugarista.Domain.Entities.OrderSplitShare> OrderSplitShares { get; }

    DbSet<Sugarista.Domain.Entities.SalonTable> SalonTables { get; }

    DbSet<Sugarista.Domain.Entities.SiteSettings> SiteSettings { get; }

    DbSet<Sugarista.Domain.Entities.PrintJob> PrintJobs { get; }

    DbSet<Sugarista.Domain.Entities.Discount> Discounts { get; }

    DbSet<Sugarista.Domain.Entities.DiscountRedemption> DiscountRedemptions { get; }

    DbSet<Sugarista.Domain.Entities.LoyaltyTransaction> LoyaltyTransactions { get; }

    DbSet<Sugarista.Domain.Entities.StampCardReward> StampCardRewards { get; }

    DbSet<Sugarista.Domain.Entities.Badge> Badges { get; }

    DbSet<Sugarista.Domain.Entities.CustomerBadge> CustomerBadges { get; }

    DbSet<Sugarista.Domain.Entities.MembershipTier> MembershipTiers { get; }

    DbSet<Sugarista.Domain.Entities.CustomerWallet> CustomerWallets { get; }

    DbSet<Sugarista.Domain.Entities.WalletTransaction> WalletTransactions { get; }

    DbSet<Sugarista.Domain.Entities.WalletTopUpPackage> WalletTopUpPackages { get; }

    DbSet<Sugarista.Domain.Entities.CustomerGroup> CustomerGroups { get; }

    DbSet<Sugarista.Domain.Entities.CustomerGroupMembership> CustomerGroupMemberships { get; }

    DbSet<Sugarista.Domain.Entities.SubscriptionPackage> SubscriptionPackages { get; }

    DbSet<Sugarista.Domain.Entities.CustomerSubscription> CustomerSubscriptions { get; }

    DbSet<Sugarista.Domain.Entities.OrderDiscount> OrderDiscounts { get; }

    DbSet<Sugarista.Domain.Entities.DiscountAttempt> DiscountAttempts { get; }

    DbSet<Sugarista.Domain.Entities.BlacklistEntry> BlacklistEntries { get; }

    DbSet<Sugarista.Domain.Entities.ReportSchedule> ReportSchedules { get; }

    DbSet<Sugarista.Domain.Entities.DrinkBuilderProfile> DrinkBuilderProfiles { get; }

    DbSet<Sugarista.Domain.Entities.DrinkBuilderGroup> DrinkBuilderGroups { get; }

    DbSet<Sugarista.Domain.Entities.DrinkBuilderOption> DrinkBuilderOptions { get; }

    DbSet<Sugarista.Domain.Entities.AbandonedCart> AbandonedCarts { get; }

    DbSet<Sugarista.Domain.Entities.TableSession> TableSessions { get; }

    DbSet<Sugarista.Domain.Entities.Account> Accounts { get; }

    DbSet<Sugarista.Domain.Entities.Partner> Partners { get; }

    DbSet<Sugarista.Domain.Entities.JournalEntry> JournalEntries { get; }

    DbSet<Sugarista.Domain.Entities.JournalLine> JournalLines { get; }

    DbSet<Sugarista.Domain.Entities.PartnerCapitalTransaction> PartnerCapitalTransactions { get; }

    DbSet<Sugarista.Domain.Entities.ProfitDistribution> ProfitDistributions { get; }

    DbSet<Sugarista.Domain.Entities.ProfitDistributionLine> ProfitDistributionLines { get; }

    DbSet<Sugarista.Domain.Entities.SmsProvider> SmsProviders { get; }

    DbSet<Sugarista.Domain.Entities.SmsSenderLine> SmsSenderLines { get; }

    DbSet<Sugarista.Domain.Entities.SmsMessage> SmsMessages { get; }

    DbSet<Sugarista.Domain.Entities.SmsCampaign> SmsCampaigns { get; }

    DbSet<Sugarista.Domain.Entities.SmsContact> SmsContacts { get; }

    DbSet<Sugarista.Domain.Entities.SmsContactGroup> SmsContactGroups { get; }

    DbSet<Sugarista.Domain.Entities.SmsContactGroupMember> SmsContactGroupMembers { get; }

    DbSet<Sugarista.Domain.Entities.SmsTemplate> SmsTemplates { get; }

    DbSet<Sugarista.Domain.Entities.SmsTemplateProviderMapping> SmsTemplateProviderMappings { get; }

    DbSet<Sugarista.Domain.Entities.SmsOptOut> SmsOptOuts { get; }

    DbSet<Sugarista.Domain.Entities.SmsProviderLog> SmsProviderLogs { get; }

    DbSet<Sugarista.Domain.Entities.AdminTooltip> AdminTooltips { get; }

    DbSet<Sugarista.Domain.Entities.Warehouse> Warehouses { get; }

    DbSet<Sugarista.Domain.Entities.WarehouseStock> WarehouseStocks { get; }

    DbSet<Sugarista.Domain.Entities.WarehouseTransfer> WarehouseTransfers { get; }

    DbSet<Sugarista.Domain.Entities.WarehouseTransferLine> WarehouseTransferLines { get; }

    DbSet<Sugarista.Domain.Entities.WarehouseVoucher> WarehouseVouchers { get; }

    DbSet<Sugarista.Domain.Entities.WarehouseVoucherLine> WarehouseVoucherLines { get; }

    DbSet<Sugarista.Domain.Entities.Affiliate> Affiliates { get; }

    DbSet<Sugarista.Domain.Entities.AffiliateTier> AffiliateTiers { get; }

    DbSet<Sugarista.Domain.Entities.AffiliateLink> AffiliateLinks { get; }

    DbSet<Sugarista.Domain.Entities.AffiliateCoupon> AffiliateCoupons { get; }

    DbSet<Sugarista.Domain.Entities.ReferralClick> ReferralClicks { get; }

    DbSet<Sugarista.Domain.Entities.AffiliateCommission> AffiliateCommissions { get; }

    DbSet<Sugarista.Domain.Entities.AffiliatePayout> AffiliatePayouts { get; }

    DbSet<Sugarista.Domain.Entities.CategoryCommission> CategoryCommissions { get; }

    DbSet<Sugarista.Domain.Entities.AffiliateMediaAsset> AffiliateMediaAssets { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
