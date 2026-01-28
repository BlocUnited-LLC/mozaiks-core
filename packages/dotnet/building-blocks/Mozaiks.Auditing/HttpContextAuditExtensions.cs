using Microsoft.AspNetCore.Http;

namespace Mozaiks.Auditing;

public static class HttpContextAuditExtensions
{
    public static void SetAdminAuditContext(
        this HttpContext httpContext,
        string action,
        string targetType,
        string targetId,
        string details)
    {
        if (httpContext is null) throw new ArgumentNullException(nameof(httpContext));

        httpContext.Items[AdminAuditConstants.HttpContextItemKey_Action] = action ?? string.Empty;
        httpContext.Items[AdminAuditConstants.HttpContextItemKey_TargetType] = targetType ?? string.Empty;
        httpContext.Items[AdminAuditConstants.HttpContextItemKey_TargetId] = targetId ?? string.Empty;
        httpContext.Items[AdminAuditConstants.HttpContextItemKey_Details] = details ?? string.Empty;
    }
}
