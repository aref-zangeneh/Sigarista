using Sugarista.Domain.Constants;

namespace Sugarista.Application.AdminUsers.Queries.GetAdminPermissionCatalog;

public record AdminPermissionGroupDto(
    string SectionId,
    string SectionLabel,
    string CategoryId,
    string CategoryLabel,
    IReadOnlyCollection<AdminPermissionItemDto> Permissions);

public record AdminPermissionItemDto(string Key, string Action, string ActionLabel);

public record GetAdminPermissionCatalogQuery : IRequest<IReadOnlyCollection<AdminPermissionGroupDto>>;

public class GetAdminPermissionCatalogQueryHandler
    : IRequestHandler<GetAdminPermissionCatalogQuery, IReadOnlyCollection<AdminPermissionGroupDto>>
{
    public Task<IReadOnlyCollection<AdminPermissionGroupDto>> Handle(
        GetAdminPermissionCatalogQuery request,
        CancellationToken cancellationToken)
    {
        // Preserve the exact logical sidebar order defined in AdminPermissions.Catalog
        var groups = AdminPermissions.Catalog
            .GroupBy(permission => permission.SectionId)
            .Select(group =>
            {
                var first = group.First();
                return new AdminPermissionGroupDto(
                    group.Key,
                    first.SectionLabel,
                    first.CategoryId,
                    first.CategoryLabel,
                    group.Select(permission => new AdminPermissionItemDto(
                        permission.Key,
                        permission.Action,
                        permission.ActionLabel)).ToList());
            })
            .ToList();

        return Task.FromResult<IReadOnlyCollection<AdminPermissionGroupDto>>(groups);
    }
}
