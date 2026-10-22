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

    public DbSet<AddonItem> AddonItems => Set<AddonItem>();

    public DbSet<MenuModifier> MenuModifiers => Set<MenuModifier>();

    public DbSet<Ingredient> Ingredients => Set<Ingredient>();

    public DbSet<Supplier> Suppliers => Set<Supplier>();

    public DbSet<IngredientSupplier> IngredientSuppliers => Set<IngredientSupplier>();

    public DbSet<DeliveryMethod> DeliveryMethods => Set<DeliveryMethod>();

    public DbSet<DeliveryMethodPackaging> DeliveryMethodPackagings => Set<DeliveryMethodPackaging>();

    public DbSet<Customer> Customers => Set<Customer>();

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

    public DbSet<AdminActivityLog> AdminActivityLogs => Set<AdminActivityLog>();

    public DbSet<PayrollPeriod> PayrollPeriods => Set<PayrollPeriod>();

    public DbSet<PayrollEntry> PayrollEntries => Set<PayrollEntry>();

    public DbSet<HrGlossaryTerm> HrGlossaryTerms => Set<HrGlossaryTerm>();

    public DbSet<RecipeItem> RecipeItems => Set<RecipeItem>();

    public DbSet<InventoryStock> InventoryStocks => Set<InventoryStock>();

    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    public DbSet<InventoryPurchase> InventoryPurchases => Set<InventoryPurchase>();

    public DbSet<MenuItemStock> MenuItemStocks => Set<MenuItemStock>();

    public DbSet<ProductionBatch> ProductionBatches => Set<ProductionBatch>();

    public DbSet<ProductWikiGuide> ProductWikiGuides => Set<ProductWikiGuide>();

    public DbSet<ProductWikiStep> ProductWikiSteps => Set<ProductWikiStep>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderLine> OrderLines => Set<OrderLine>();

    public DbSet<OrderPayment> OrderPayments => Set<OrderPayment>();

    public DbSet<OrderSplitShare> OrderSplitShares => Set<OrderSplitShare>();

    public DbSet<SalonTable> SalonTables => Set<SalonTable>();

    public DbSet<SiteSettings> SiteSettings => Set<SiteSettings>();

    public DbSet<TableSession> TableSessions => Set<TableSession>();

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<Partner> Partners => Set<Partner>();

    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();

    public DbSet<JournalLine> JournalLines => Set<JournalLine>();

    public DbSet<PartnerCapitalTransaction> PartnerCapitalTransactions => Set<PartnerCapitalTransaction>();

    public DbSet<ProfitDistribution> ProfitDistributions => Set<ProfitDistribution>();

    public DbSet<ProfitDistributionLine> ProfitDistributionLines => Set<ProfitDistributionLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
