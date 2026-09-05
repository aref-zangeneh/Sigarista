namespace Sugarista.Application.Common.Interfaces;

public interface ICurrentUserService
{
    string? UserId { get; }

    bool IsAuthenticated { get; }

    bool IsSuperAdmin { get; }

    IReadOnlyCollection<string> Permissions { get; }

    bool HasPermission(string permission);
}
