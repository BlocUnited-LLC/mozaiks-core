namespace Mozaiks.Auth;

public static class MozaiksAuthDefaults
{
    public const string UserScheme = "Bearer";
    public const string InternalScheme = "InternalBearer";

    public const string RequirePlatformAdminPolicy = "RequirePlatformAdmin";
    public const string RequireSuperAdminPolicy = "RequireSuperAdmin";
    public const string RequireMfaPolicy = "RequireMfa";
    public const string InternalServicePolicy = "InternalService";
}
