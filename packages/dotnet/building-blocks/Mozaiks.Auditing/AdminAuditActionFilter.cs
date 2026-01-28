using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Security.Claims;
using Mozaiks.Auth;

namespace Mozaiks.Auditing;

public sealed class AdminAuditActionFilter : IAsyncActionFilter
{
    private readonly IMongoDatabase _database;
    private readonly IOptions<AdminAuditOptions> _options;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IUserContextAccessor _userContextAccessor;
    private readonly ILogger<AdminAuditActionFilter> _logger;

    public AdminAuditActionFilter(
        IMongoDatabase database,
        IOptions<AdminAuditOptions> options,
        IHostEnvironment hostEnvironment,
        IUserContextAccessor userContextAccessor,
        ILogger<AdminAuditActionFilter> logger)
    {
        _database = database;
        _options = options;
        _hostEnvironment = hostEnvironment;
        _userContextAccessor = userContextAccessor;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;

        if (!httpContext.User.Identity?.IsAuthenticated ?? true)
        {
            await next();
            return;
        }

        var userContext = _userContextAccessor.GetUser(httpContext.User);
        if (userContext is null || !HasAdminRole(userContext))
        {
            await next();
            return;
        }

        // Only audit state-changing verbs.
        var method = httpContext.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method))
        {
            await next();
            return;
        }

        var stopwatch = Stopwatch.StartNew();

        Exception? exception = null;
        ActionExecutedContext? executed = null;

        try
        {
            executed = await next();
            exception = executed.ExceptionHandled ? null : executed.Exception;
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        // If the controller already logged an audit entry during execution, don't double-log.
        if (httpContext.Items.ContainsKey(AdminAuditConstants.HttpContextItemKey_AuditAlreadyLogged))
        {
            if (exception is not null)
            {
                ExceptionDispatchInfo.Capture(exception).Throw();
            }

            return;
        }

        try
        {
            var actorUserId = userContext.UserId;
            var actorEmail = userContext.Email;

            var actionName = TryGetActionName(context);
            var targetType = TryGetTargetType(context);
            var targetId = TryGetTargetId(httpContext);

            var overrideAction = TryGetContextItemString(httpContext, AdminAuditConstants.HttpContextItemKey_Action);
            if (!string.IsNullOrWhiteSpace(overrideAction))
            {
                actionName = overrideAction!;
            }

            var overrideTargetType = TryGetContextItemString(httpContext, AdminAuditConstants.HttpContextItemKey_TargetType);
            if (!string.IsNullOrWhiteSpace(overrideTargetType))
            {
                targetType = overrideTargetType!;
            }

            var overrideTargetId = TryGetContextItemString(httpContext, AdminAuditConstants.HttpContextItemKey_TargetId);
            if (!string.IsNullOrWhiteSpace(overrideTargetId))
            {
                targetId = overrideTargetId!;
            }

            var statusCode = executed?.HttpContext.Response.StatusCode ?? StatusCodes.Status500InternalServerError;
            var result = exception is null && statusCode < 400 ? "success" : "fail";

            var resolvedServiceName = (_options.Value.ServiceName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(resolvedServiceName))
            {
                resolvedServiceName = _hostEnvironment.ApplicationName ?? string.Empty;
            }
            if (string.IsNullOrWhiteSpace(resolvedServiceName))
            {
                resolvedServiceName = "unknown";
            }

            var correlationId = GetCorrelationId(httpContext);
            var traceId = Activity.Current?.TraceId.ToString() ?? string.Empty;
            var spanId = Activity.Current?.SpanId.ToString() ?? string.Empty;

            var autoDetails = $"auto method={method} path={httpContext.Request.Path} status={statusCode} elapsedMs={stopwatch.ElapsedMilliseconds} requestId={httpContext.TraceIdentifier}";
            if (exception is not null)
            {
                autoDetails += $" exception={exception.GetType().Name}: {exception.Message}";
            }

            var extraDetails = TryGetContextItemString(httpContext, AdminAuditConstants.HttpContextItemKey_Details);
            var details = string.IsNullOrWhiteSpace(extraDetails)
                ? autoDetails
                : $"{extraDetails} | {autoDetails}";

            var collectionName = (_options.Value.CollectionName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(collectionName))
            {
                collectionName = AdminAuditConstants.DefaultCollectionName;
            }

            var auditCollection = _database.GetCollection<BsonDocument>(collectionName);
            var entry = new BsonDocument
            {
                { "timestamp", DateTime.UtcNow },
                { "action", actionName },
                { "targetType", targetType },
                { "targetId", targetId },
                { "adminUserId", actorUserId },
                { "adminEmail", actorEmail },
                { "service", resolvedServiceName },
                { "method", method },
                { "path", httpContext.Request.Path.ToString() },
                { "statusCode", statusCode },
                { "result", result },
                { "correlationId", correlationId },
                { "requestId", httpContext.TraceIdentifier },
                { "traceId", traceId },
                { "spanId", spanId },
                { "ip", httpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty },
                { "userAgent", httpContext.Request.Headers.UserAgent.ToString() },
                { "details", details }
            };

            await auditCollection.InsertOneAsync(entry, cancellationToken: httpContext.RequestAborted);

            httpContext.Items[AdminAuditConstants.HttpContextItemKey_AuditAlreadyLogged] = true;
        }
        catch (Exception logEx)
        {
            _logger.LogError(logEx, "Failed to log centralized admin audit entry");
        }

        if (exception is not null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }

    private static bool HasAdminRole(UserContext userContext)
        => userContext.Roles.Any(r => string.Equals(r, "SuperAdmin", StringComparison.OrdinalIgnoreCase)
                                      || string.Equals(r, "Admin", StringComparison.OrdinalIgnoreCase));

    private static string TryGetActionName(ActionExecutingContext context)
    {
        if (context.ActionDescriptor is ControllerActionDescriptor cad)
        {
            return $"{cad.ControllerName}.{cad.ActionName}";
        }

        return context.ActionDescriptor.DisplayName ?? "unknown";
    }

    private static string TryGetTargetType(ActionExecutingContext context)
    {
        if (context.ActionDescriptor is ControllerActionDescriptor cad)
        {
            return cad.ControllerName;
        }

        return "unknown";
    }

    private static string TryGetTargetId(HttpContext httpContext)
    {
        var routeValues = httpContext.Request.RouteValues;

        var keysToTry = new[] { "id", "appId", "userId", "targetId" };
        foreach (var key in keysToTry)
        {
            if (routeValues.TryGetValue(key, out var val) && val is not null)
            {
                var s = val.ToString();
                if (!string.IsNullOrWhiteSpace(s)) return s!;
            }
        }

        return string.Empty;
    }

    private static string GetCorrelationId(HttpContext httpContext)
    {
        var headers = httpContext.Request.Headers;

        var fromHeader = headers.TryGetValue("X-Correlation-ID", out var correlation)
            ? correlation.ToString()
            : string.Empty;
        if (!string.IsNullOrWhiteSpace(fromHeader)) return fromHeader;

        var fromRequestId = headers.TryGetValue("X-Request-ID", out var requestId)
            ? requestId.ToString()
            : string.Empty;
        if (!string.IsNullOrWhiteSpace(fromRequestId)) return fromRequestId;

        var fromActivity = Activity.Current?.TraceId.ToString() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(fromActivity)) return fromActivity;

        return httpContext.TraceIdentifier;
    }

    private static string? TryGetContextItemString(HttpContext httpContext, string key)
    {
        if (!httpContext.Items.TryGetValue(key, out var raw) || raw is null) return null;

        return raw switch
        {
            string s => s,
            _ => raw.ToString()
        };
    }
}
