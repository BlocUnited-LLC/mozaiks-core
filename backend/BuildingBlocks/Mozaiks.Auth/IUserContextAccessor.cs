using System.Security.Claims;

namespace Mozaiks.Auth;

public interface IUserContextAccessor
{
    UserContext? GetUser(ClaimsPrincipal? principal = null);
    UserContext GetRequiredUser(ClaimsPrincipal? principal = null);
}

