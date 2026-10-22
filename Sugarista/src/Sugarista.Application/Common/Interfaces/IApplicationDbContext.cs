namespace Sugarista.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Sugarista.Domain.Entities.AdminUser> AdminUsers { get; }

    DbSet<Sugarista.Domain.Entities.AdminUserPermission> AdminUserPermissions { get; }

    DbSet<Sugarista.Domain.Entities.Category> Categories { get; }

    DbSet<Sugarista.Domain.Entities.CostControlBoard> CostControlBoards { get; }

    DbSet<Sugarista.Domain.Entities.MenuItem> MenuItems { get; }

    DbSet<Sugarista.Domain.Entities.AddonItem> AddonItems { get; }

    DbSet<Sugarista.Domain.Entities.MenuModifier> MenuModifiers { get; }

    DbSet<Sugarista.Domain.Entities.Ingredient> Ingredients { get; }

    DbSet<Sugarista.Domain.Entities.Supplier> Suppliers { get; }

    DbSet<Sugarista.Domain.Entities.DeliveryMethod> DeliveryMethods { get; }

    DbSet<Sugarista.Domain.Entities.Customer> Customers { get; }

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

    DbSet<Sugarista.Domain.Entities.AdminActivityLog> AdminActivityLogs { get; }

    DbSet<Sugarista.Domain.Entities.PayrollPeriod> PayrollPeriods { get; }

    DbSet<Sugarista.Domain.Entities.PayrollEntry> PayrollEntries { get; }

    DbSet<Sugarista.Domain.Entities.HrGlossaryTerm> HrGlossaryTerms { get; }

    DbSet<Sugarista.Domain.Entities.DeliveryMethodPackaging> DeliveryMethodPackagings { get; }

    DbSet<Sugarista.Domain.Entities.IngredientSupplier> IngredientSuppliers { get; }

    DbSet<Sugarista.Domain.Entities.RecipeItem> RecipeItems { get; }

    DbSet<Sugarista.Domain.Entities.InventoryStock> InventoryStocks { get; }

    DbSet<Sugarista.Domain.Entities.StockMovement> StockMovements { get; }

    DbSet<Sugarista.Domain.Entities.InventoryPurchase> InventoryPurchases { get; }

    DbSet<Sugarista.Domain.Entities.MenuItemStock> MenuItemStocks { get; }

    DbSet<Sugarista.Domain.Entities.ProductionBatch> ProductionBatches { get; }

    DbSet<Sugarista.Domain.Entities.ProductWikiGuide> ProductWikiGuides { get; }

    DbSet<Sugarista.Domain.Entities.ProductWikiStep> ProductWikiSteps { get; }

    DbSet<Sugarista.Domain.Entities.Order> Orders { get; }

    DbSet<Sugarista.Domain.Entities.OrderLine> OrderLines { get; }

    DbSet<Sugarista.Domain.Entities.OrderPayment> OrderPayments { get; }

    DbSet<Sugarista.Domain.Entities.OrderSplitShare> OrderSplitShares { get; }

    DbSet<Sugarista.Domain.Entities.SalonTable> SalonTables { get; }

    DbSet<Sugarista.Domain.Entities.SiteSettings> SiteSettings { get; }

    DbSet<Sugarista.Domain.Entities.TableSession> TableSessions { get; }

    DbSet<Sugarista.Domain.Entities.Account> Accounts { get; }

    DbSet<Sugarista.Domain.Entities.Partner> Partners { get; }

    DbSet<Sugarista.Domain.Entities.JournalEntry> JournalEntries { get; }

    DbSet<Sugarista.Domain.Entities.JournalLine> JournalLines { get; }

    DbSet<Sugarista.Domain.Entities.PartnerCapitalTransaction> PartnerCapitalTransactions { get; }

    DbSet<Sugarista.Domain.Entities.ProfitDistribution> ProfitDistributions { get; }

    DbSet<Sugarista.Domain.Entities.ProfitDistributionLine> ProfitDistributionLines { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
