using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Mozaiks.Auth.Authorization;

public sealed class MfaHandler : AuthorizationHandler<MfaRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, MfaRequirement requirement)
    {
        var user = context.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        // Common patterns:
        // - amr: ["pwd","mfa"] (array -> multiple claims or a single claim depending on handler)
        // - acr: Authentication Context Class Reference which may embed "mfa"
        if (HasAmrMfa(user) || HasAcrMfa(user))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    private static bool HasAmrMfa(ClaimsPrincipal user)
    {
        var amrValues = user.Claims
            .Where(c => string.Equals(c.Type, "amr", StringComparison.OrdinalIgnoreCase))
            .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToArray();

        return amrValues.Any(v => string.Equals(v, "mfa", StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasAcrMfa(ClaimsPrincipal user)
    {
        var acr = user.Claims.FirstOrDefault(c => string.Equals(c.Type, "acr", StringComparison.OrdinalIgnoreCase))?.Value;

        return !string.IsNullOrWhiteSpace(acr) && acr.Contains("mfa", StringComparison.OrdinalIgnoreCase);
    }
}
