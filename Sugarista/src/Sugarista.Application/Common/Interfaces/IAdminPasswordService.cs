using Sugarista.Domain.Entities;

namespace Sugarista.Application.Common.Interfaces;

public interface IAdminPasswordService
{
    string HashPassword(AdminUser user, string password);

    bool VerifyPassword(AdminUser user, string password);
}
