using Sugarista.Domain.Constants;

namespace Sugarista.Application.AdminUsers.Queries.GetAdminPermissionCatalog;

public record AdminPermissionGroupDto(string SectionId, string SectionLabel, IReadOnlyCollection<AdminPermissionItemDto> Permissions);

public record AdminPermissionItemDto(string Key, string Action, string ActionLabel);

public record GetAdminPermissionCatalogQuery : IRequest<IReadOnlyCollection<AdminPermissionGroupDto>>;

public class GetAdminPermissionCatalogQueryHandler
    : IRequestHandler<GetAdminPermissionCatalogQuery, IReadOnlyCollection<AdminPermissionGroupDto>>
{
    public Task<IReadOnlyCollection<AdminPermissionGroupDto>> Handle(
        GetAdminPermissionCatalogQuery request,
        CancellationToken cancellationToken)
    {
        var groups = AdminPermissions.Catalog
            .GroupBy(permission => permission.SectionId)
            .Select(group => new AdminPermissionGroupDto(
                group.Key,
                group.First().SectionLabel,
                group.Select(permission => new AdminPermissionItemDto(
                    permission.Key,
                    permission.Action,
                    permission.ActionLabel)).ToList()))
            .OrderBy(group => group.SectionLabel)
            .ToList();

        return Task.FromResult<IReadOnlyCollection<AdminPermissionGroupDto>>(groups);
    }
}
