namespace Mozaiks.Auth;

public sealed record UserContext(
    string UserId,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles,
    string TenantId,
    bool IsSuperAdmin,
    IReadOnlyDictionary<string, IReadOnlyList<string>> RawClaims);

