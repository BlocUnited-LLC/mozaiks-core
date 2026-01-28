using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Mozaiks.Auth;

public sealed class UserContextAccessor : IUserContextAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly MozaiksAuthOptions _options;
    private readonly ILogger<UserContextAccessor> _logger;

    public UserContextAccessor(
        IHttpContextAccessor httpContextAccessor,
        MozaiksAuthOptions options,
        ILogger<UserContextAccessor> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _options = options;
        _logger = logger;
    }

    public UserContext? GetUser(ClaimsPrincipal? principal = null)
    {
        principal ??= _httpContextAccessor.HttpContext?.User;

        if (principal?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var userId = GetClaimValue(principal, _options.UserIdClaim);
        if (string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogWarning("Authenticated principal missing required user id claim '{UserIdClaim}'", _options.UserIdClaim);
            return null;
        }

        var email = GetEmail(principal);
        var displayName = GetDisplayName(principal, email, userId);
        var roles = GetStringValues(principal, _options.RolesClaim);
        var tenantId = GetTenantId(principal, _options.TenantClaim);
        var isSuperAdmin = IsSuperAdmin(roles);
        var rawClaims = ToRawClaims(principal.Claims);

        return new UserContext(
            UserId: userId,
            Email: email,
            DisplayName: displayName,
            Roles: roles,
            TenantId: tenantId,
            IsSuperAdmin: isSuperAdmin,
            RawClaims: rawClaims);
    }

    public UserContext GetRequiredUser(ClaimsPrincipal? principal = null)
        => GetUser(principal) ?? throw new InvalidOperationException("Authenticated user context is not available.");

    private string GetEmail(ClaimsPrincipal principal)
    {
        foreach (var claimType in _options.EmailClaimCandidates)
        {
            var values = GetStringValues(principal, claimType);
            var email = values.FirstOrDefault(v => v.Contains('@'));
            if (!string.IsNullOrWhiteSpace(email))
            {
                return email;
            }
        }

        return string.Empty;
    }

    private static string GetDisplayName(ClaimsPrincipal principal, string email, string userId)
    {
        var name = GetClaimValue(principal, "name");
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name!;
        }

        var given = GetClaimValue(principal, "given_name") ?? string.Empty;
        var family = GetClaimValue(principal, "family_name") ?? string.Empty;
        var combined = $"{given} {family}".Trim();
        if (!string.IsNullOrWhiteSpace(combined))
        {
            return combined;
        }

        var preferred = GetClaimValue(principal, "preferred_username");
        if (!string.IsNullOrWhiteSpace(preferred))
        {
            return preferred!;
        }

        return !string.IsNullOrWhiteSpace(email) ? email : userId;
    }

    private static string GetTenantId(ClaimsPrincipal principal, string tenantClaim)
    {
        return !string.IsNullOrWhiteSpace(tenantClaim)
            ? GetClaimValue(principal, tenantClaim) ?? string.Empty
            : string.Empty;
    }

    private bool IsSuperAdmin(IReadOnlyList<string> roles)
    {
        if (!string.IsNullOrWhiteSpace(_options.SuperAdminRole)
            && roles.Any(r => string.Equals(r, _options.SuperAdminRole, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }

    private static string? GetClaimValue(ClaimsPrincipal principal, string claimType)
    {
        foreach (var claim in principal.Claims)
        {
            if (string.Equals(claim.Type, claimType, StringComparison.OrdinalIgnoreCase))
            {
                return claim.Value;
            }
        }

        return null;
    }

    private static IReadOnlyList<string> GetStringValues(ClaimsPrincipal principal, string claimType)
    {
        var results = new List<string>();
        foreach (var claim in principal.Claims)
        {
            if (string.Equals(claim.Type, claimType, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(claim.Value))
            {
                results.Add(claim.Value);
            }
        }

        return results
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> ToRawClaims(IEnumerable<Claim> claims)
    {
        return claims
            .GroupBy(c => c.Type, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<string>)g.Select(c => c.Value).Where(v => !string.IsNullOrWhiteSpace(v)).Distinct().ToArray(),
                StringComparer.OrdinalIgnoreCase);
    }
}
