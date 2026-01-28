namespace Mozaiks.Auditing;

public static class AdminAuditConstants
{
    public const string DefaultCollectionName = "admin_audit_log";

    public const string HttpContextItemKey_Action = "Mozaiks.Auditing.Action";
    public const string HttpContextItemKey_TargetType = "Mozaiks.Auditing.TargetType";
    public const string HttpContextItemKey_TargetId = "Mozaiks.Auditing.TargetId";
    public const string HttpContextItemKey_Details = "Mozaiks.Auditing.Details";

    // Used by centralized audit logging to prevent double-inserts when a controller
    // already logged an audit entry manually.
    public const string HttpContextItemKey_AuditAlreadyLogged = "Mozaiks.Auditing.AuditAlreadyLogged";
}
