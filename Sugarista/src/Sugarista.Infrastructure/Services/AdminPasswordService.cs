using Microsoft.AspNetCore.Identity;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Services;

public class AdminPasswordService : IAdminPasswordService
{
    private readonly PasswordHasher<AdminUser> _passwordHasher = new();

    public string HashPassword(AdminUser user, string password)
    {
        return _passwordHasher.HashPassword(user, password);
    }

    public bool VerifyPassword(AdminUser user, string password)
    {
        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);

        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
