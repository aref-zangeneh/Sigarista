using Sugarista.Domain.Entities;

namespace Sugarista.Application.Common.Interfaces;

public interface IJwtTokenGenerator
{
    string GenerateToken(AdminUser user);
}
