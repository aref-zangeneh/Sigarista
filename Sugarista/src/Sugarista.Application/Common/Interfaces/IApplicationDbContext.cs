namespace Sugarista.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Sugarista.Domain.Entities.AdminUser> AdminUsers { get; }

    DbSet<Sugarista.Domain.Entities.Category> Categories { get; }

    DbSet<Sugarista.Domain.Entities.MenuItem> MenuItems { get; }

    DbSet<Sugarista.Domain.Entities.AddonItem> AddonItems { get; }

    DbSet<Sugarista.Domain.Entities.MenuModifier> MenuModifiers { get; }

    DbSet<Sugarista.Domain.Entities.Ingredient> Ingredients { get; }

    DbSet<Sugarista.Domain.Entities.RecipeItem> RecipeItems { get; }

    DbSet<Sugarista.Domain.Entities.InventoryStock> InventoryStocks { get; }

    DbSet<Sugarista.Domain.Entities.StockMovement> StockMovements { get; }

    DbSet<Sugarista.Domain.Entities.Order> Orders { get; }

    DbSet<Sugarista.Domain.Entities.OrderLine> OrderLines { get; }

    DbSet<Sugarista.Domain.Entities.SalonTable> SalonTables { get; }

    DbSet<Sugarista.Domain.Entities.SiteSettings> SiteSettings { get; }

    DbSet<Sugarista.Domain.Entities.TableSession> TableSessions { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
