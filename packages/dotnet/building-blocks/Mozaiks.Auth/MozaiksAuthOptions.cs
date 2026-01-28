namespace Mozaiks.Auth;

public sealed class MozaiksAuthOptions
{
    public string Provider { get; init; } = "ciam";

    // OIDC (CIAM) mode
    public string? Authority { get; init; }
    public string? TenantId { get; init; }
    public string? MetadataAddress { get; init; }

    // JWKS-only mode (self-hosting)
    public string? Issuer { get; init; }
    public string? JwksUrl { get; init; }

    public string Audience { get; init; } = string.Empty;
    public string RequiredScope { get; init; } = string.Empty;
    public string[] AllowedClientIds { get; init; } = Array.Empty<string>();

    public string UserIdClaim { get; init; } = "sub";
    public string[] EmailClaimCandidates { get; init; } = new[] { "email", "emails" };
    public string RolesClaim { get; init; } = "roles";
    public string TenantClaim { get; init; } = "tid";

    public string SuperAdminRole { get; init; } = "SuperAdmin";

    public static MozaiksAuthOptions FromEnvironment()
    {
        string? Get(string key) => Normalize(Environment.GetEnvironmentVariable(key));

        var provider = Get("AUTH_PROVIDER") ?? "ciam";

        var authority = Get("AUTH_AUTHORITY");
        var tenantId = Get("AUTH_TENANT_ID");
        var metadataAddress = Get("AUTH_METADATA_ADDRESS");

        var issuer = Get("AUTH_ISSUER");
        var jwksUrl = Get("AUTH_JWKS_URL");

        var audience = Get("AUTH_AUDIENCE") ?? string.Empty;
        var requiredScope = Get("AUTH_REQUIRED_SCOPE") ?? string.Empty;
        var allowedClientIds = ParseCsv(Get("AUTH_ALLOWED_CLIENT_IDS"));

        var userIdClaim = Get("AUTH_USER_ID_CLAIM") ?? "sub";
        var emailClaimCandidates = ParseCsv(Get("AUTH_EMAIL_CLAIM"));
        if (emailClaimCandidates.Length == 0)
        {
            emailClaimCandidates = new[] { "email" };
        }

        var rolesClaim = Get("AUTH_ROLES_CLAIM") ?? "roles";
        var tenantClaim = Get("AUTH_TENANT_CLAIM") ?? "tid";
        var superAdminRole = Get("AUTH_SUPERADMIN_ROLE") ?? "SuperAdmin";

        return new MozaiksAuthOptions
        {
            Provider = provider,
            Authority = authority,
            TenantId = tenantId,
            MetadataAddress = metadataAddress,
            Issuer = issuer,
            JwksUrl = jwksUrl,
            Audience = audience,
            RequiredScope = requiredScope,
            AllowedClientIds = allowedClientIds,
            UserIdClaim = userIdClaim,
            EmailClaimCandidates = emailClaimCandidates,
            RolesClaim = rolesClaim,
            TenantClaim = tenantClaim,
            SuperAdminRole = superAdminRole
        };
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string[] ParseCsv(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Normalize)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
