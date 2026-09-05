namespace Sugarista.Application.Common.Interfaces;

public interface IAdminAuthorizationService
{
    Task<IReadOnlyCollection<string>> GetPermissionsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> IsSuperAdminAsync(Guid userId, CancellationToken cancellationToken = default);
}
