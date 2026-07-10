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

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<MenuItem> MenuItems => Set<MenuItem>();

    public DbSet<AddonItem> AddonItems => Set<AddonItem>();

    public DbSet<MenuModifier> MenuModifiers => Set<MenuModifier>();

    public DbSet<Ingredient> Ingredients => Set<Ingredient>();

    public DbSet<RecipeItem> RecipeItems => Set<RecipeItem>();

    public DbSet<InventoryStock> InventoryStocks => Set<InventoryStock>();

    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderLine> OrderLines => Set<OrderLine>();

    public DbSet<SalonTable> SalonTables => Set<SalonTable>();

    public DbSet<SiteSettings> SiteSettings => Set<SiteSettings>();

    public DbSet<TableSession> TableSessions => Set<TableSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
