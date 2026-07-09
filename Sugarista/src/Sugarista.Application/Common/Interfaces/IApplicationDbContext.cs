namespace Sugarista.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Sugarista.Domain.Entities.AdminUser> AdminUsers { get; }

    DbSet<Sugarista.Domain.Entities.Category> Categories { get; }

    DbSet<Sugarista.Domain.Entities.MenuItem> MenuItems { get; }

    DbSet<Sugarista.Domain.Entities.SalonTable> SalonTables { get; }

    DbSet<Sugarista.Domain.Entities.SiteSettings> SiteSettings { get; }

    DbSet<Sugarista.Domain.Entities.TableSession> TableSessions { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
