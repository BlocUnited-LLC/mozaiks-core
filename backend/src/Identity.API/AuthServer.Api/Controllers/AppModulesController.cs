using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AuthServer.Api.Models;
using AuthServer.Api.Repository.Interfaces;
using AuthServer.Api.Services;
using AuthServer.Api.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mozaiks.Auth;

namespace AuthServer.Api.Controllers
{
    [ApiController]
    [Route("api/apps/{appId}/modules")]
    [Authorize]
    public sealed class AppModulesController : ControllerBase
    {
        private readonly MozaiksAppService _apps;
        private readonly ITeamMembersRepository _teamMembers;
        private readonly IAppModuleProxyService _proxy;
        private readonly IAppModuleProxyAuditRepository _audit;
        private readonly StructuredLogEmitter _logs;
        private readonly IUserContextAccessor _userContextAccessor;

        public AppModulesController(
            MozaiksAppService apps,
            ITeamMembersRepository teamMembers,
            IAppModuleProxyService proxy,
            IAppModuleProxyAuditRepository audit,
            StructuredLogEmitter logs,
            IUserContextAccessor userContextAccessor)
        {
            _apps = apps;
            _teamMembers = teamMembers;
            _proxy = proxy;
            _audit = audit;
            _logs = logs;
            _userContextAccessor = userContextAccessor;
        }

        [HttpGet]
        public async Task<IActionResult> ListModules(string appId, CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId) || userId == "unknown")
            {
                return Unauthorized(new { error = "Unauthorized", reason = "MissingUserId" });
            }

            var correlationId = GetOrCreateCorrelationId();
            Response.Headers["x-correlation-id"] = correlationId;

            Activity.Current?.SetTag("correlationId", correlationId);
            Activity.Current?.SetTag("userId", userId);
            Activity.Current?.SetTag("appId", appId);

            var context = new StructuredLogContext { CorrelationId = correlationId, UserId = userId, AppId = appId };
            _logs.Info("Apps.Modules.List.Requested", context);

            var access = await AuthorizeAppAccessAsync(appId, userId, correlationId, cancellationToken);
            if (access.Result is not null)
            {
                return access.Result;
            }

            var result = await _proxy.SendAsync(appId, "/__mozaiks/admin/modules", HttpMethod.Get, null, correlationId, cancellationToken);
            return ToProxyActionResult(result, correlationId);
        }

        [HttpGet("{moduleId}/settings")]
        public async Task<IActionResult> GetModuleSettings(string appId, string moduleId, CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId) || userId == "unknown")
            {
                return Unauthorized(new { error = "Unauthorized", reason = "MissingUserId" });
            }

            if (string.IsNullOrWhiteSpace(moduleId))
            {
                return BadRequest(new { error = "bad_request", message = "moduleId is required" });
            }

            var correlationId = GetOrCreateCorrelationId();
            Response.Headers["x-correlation-id"] = correlationId;

            var access = await AuthorizeAppAccessAsync(appId, userId, correlationId, cancellationToken);
            if (access.Result is not null)
            {
                return access.Result;
            }

            var path = $"/__mozaiks/admin/modules/{Uri.EscapeDataString(moduleId)}/settings";
            var result = await _proxy.SendAsync(appId, path, HttpMethod.Get, null, correlationId, cancellationToken);
            return ToProxyActionResult(result, correlationId);
        }

        [HttpPut("{moduleId}/settings")]
        public async Task<IActionResult> UpdateModuleSettings(string appId, string moduleId, [FromBody] JsonElement requestBody, CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId) || userId == "unknown")
            {
                return Unauthorized(new { error = "Unauthorized", reason = "MissingUserId" });
            }

            if (string.IsNullOrWhiteSpace(moduleId))
            {
                return BadRequest(new { error = "bad_request", message = "moduleId is required" });
            }

            var correlationId = GetOrCreateCorrelationId();
            Response.Headers["x-correlation-id"] = correlationId;

            var access = await AuthorizeAppAccessAsync(appId, userId, correlationId, cancellationToken);
            if (access.Result is not null)
            {
                return access.Result;
            }

            var jsonBytes = Encoding.UTF8.GetBytes(requestBody.GetRawText());
            var path = $"/__mozaiks/admin/modules/{Uri.EscapeDataString(moduleId)}/settings";
            var result = await _proxy.SendAsync(appId, path, HttpMethod.Put, jsonBytes, correlationId, cancellationToken);

            await WriteAuditEventAsync(
                appId,
                moduleId,
                actionId: null,
                operation: "settings_update",
                actorUserId: userId,
                actorRole: access.ActorRole,
                correlationId,
                path,
                requestBytes: jsonBytes.LongLength,
                responseBytes: result.Body?.LongLength ?? 0,
                succeeded: result.Succeeded,
                statusCode: result.UpstreamStatusCode ?? 0,
                errorMessage: result.Succeeded ? null : result.ErrorMessage,
                cancellationToken);

            return ToProxyActionResult(result, correlationId);
        }

        [HttpGet("{moduleId}/status")]
        public async Task<IActionResult> GetModuleStatus(string appId, string moduleId, CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId) || userId == "unknown")
            {
                return Unauthorized(new { error = "Unauthorized", reason = "MissingUserId" });
            }

            if (string.IsNullOrWhiteSpace(moduleId))
            {
                return BadRequest(new { error = "bad_request", message = "moduleId is required" });
            }

            var correlationId = GetOrCreateCorrelationId();
            Response.Headers["x-correlation-id"] = correlationId;

            var access = await AuthorizeAppAccessAsync(appId, userId, correlationId, cancellationToken);
            if (access.Result is not null)
            {
                return access.Result;
            }

            var path = $"/__mozaiks/admin/modules/{Uri.EscapeDataString(moduleId)}/status";
            var result = await _proxy.SendAsync(appId, path, HttpMethod.Get, null, correlationId, cancellationToken);
            return ToProxyActionResult(result, correlationId);
        }

        [HttpPost("{moduleId}/actions/{actionId}")]
        public async Task<IActionResult> InvokeAction(string appId, string moduleId, string actionId, [FromBody] JsonElement requestBody, CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId) || userId == "unknown")
            {
                return Unauthorized(new { error = "Unauthorized", reason = "MissingUserId" });
            }

            if (string.IsNullOrWhiteSpace(moduleId) || string.IsNullOrWhiteSpace(actionId))
            {
                return BadRequest(new { error = "bad_request", message = "moduleId and actionId are required" });
            }

            var correlationId = GetOrCreateCorrelationId();
            Response.Headers["x-correlation-id"] = correlationId;

            var access = await AuthorizeAppAccessAsync(appId, userId, correlationId, cancellationToken);
            if (access.Result is not null)
            {
                return access.Result;
            }

            var jsonBytes = Encoding.UTF8.GetBytes(requestBody.GetRawText());
            var path = $"/__mozaiks/admin/modules/{Uri.EscapeDataString(moduleId)}/actions/{Uri.EscapeDataString(actionId)}";
            var result = await _proxy.SendAsync(appId, path, HttpMethod.Post, jsonBytes, correlationId, cancellationToken);

            await WriteAuditEventAsync(
                appId,
                moduleId,
                actionId,
                operation: "action_invoke",
                actorUserId: userId,
                actorRole: access.ActorRole,
                correlationId,
                path,
                requestBytes: jsonBytes.LongLength,
                responseBytes: result.Body?.LongLength ?? 0,
                succeeded: result.Succeeded,
                statusCode: result.UpstreamStatusCode ?? 0,
                errorMessage: result.Succeeded ? null : result.ErrorMessage,
                cancellationToken);

            return ToProxyActionResult(result, correlationId);
        }

        private sealed class AppAccessResult
        {
            public IActionResult? Result { get; init; }
            public string ActorRole { get; init; } = "team";
        }

        private sealed class ModuleProxyErrorResponse
        {
            [JsonPropertyName("error")]
            public string Error { get; init; } = "module_proxy_failed";

            [JsonPropertyName("message")]
            public string Message { get; init; } = "Module request failed.";

            [JsonPropertyName("correlationId")]
            public string CorrelationId { get; init; } = string.Empty;

            [JsonPropertyName("upstreamStatusCode")]
            [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
            public int? UpstreamStatusCode { get; init; }
        }

        private async Task<AppAccessResult> AuthorizeAppAccessAsync(string appId, string userId, string correlationId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(appId))
            {
                return new AppAccessResult { Result = BadRequest(new { error = "InvalidAppId", correlationId }) };
            }

            var app = await _apps.GetByIdAsync(appId);
            if (app is null || app.IsDeleted || app.Status == AppStatus.Deleted)
            {
                return new AppAccessResult { Result = NotFound(new { error = "NotFound", correlationId }) };
            }

            if (IsPlatformAdmin())
            {
                return new AppAccessResult { ActorRole = "admin" };
            }

            if (string.Equals(app.OwnerUserId, userId, StringComparison.OrdinalIgnoreCase))
            {
                return new AppAccessResult { ActorRole = "creator" };
            }

            var member = await _teamMembers.GetByAppAndUserIdAsync(appId, userId);
            if (member is not null)
            {
                return new AppAccessResult { ActorRole = "team" };
            }

            return new AppAccessResult { Result = Forbid() };
        }

        private IActionResult ToProxyActionResult(AppModuleProxyResult result, string correlationId)
        {
            if (result.Succeeded && result.Body is not null)
            {
                var upstreamStatusCode = result.UpstreamStatusCode ?? 200;
                if (upstreamStatusCode == 204 || result.Body.Length == 0)
                {
                    return StatusCode(upstreamStatusCode);
                }

                return new ContentResult
                {
                    StatusCode = upstreamStatusCode,
                    ContentType = string.IsNullOrWhiteSpace(result.ContentType) ? "application/json" : result.ContentType!,
                    Content = Encoding.UTF8.GetString(result.Body)
                };
            }

            var statusCode = result.FailureKind switch
            {
                AppModuleProxyFailureKind.NotConfigured => 409,
                AppModuleProxyFailureKind.InvalidConfiguration => 500,
                AppModuleProxyFailureKind.CircuitOpen => 503,
                AppModuleProxyFailureKind.Timeout => 504,
                AppModuleProxyFailureKind.NetworkError => 502,
                AppModuleProxyFailureKind.ResponseTooLarge => 502,
                AppModuleProxyFailureKind.UpstreamError => result.UpstreamStatusCode is >= 400 and < 500 ? result.UpstreamStatusCode.Value : 502,
                _ => 500
            };

            var payload = new ModuleProxyErrorResponse
            {
                Error = result.ErrorCode ?? "module_proxy_failed",
                Message = result.ErrorMessage ?? "Module request failed.",
                CorrelationId = correlationId,
                UpstreamStatusCode = result.UpstreamStatusCode
            };

            return StatusCode(statusCode, payload);
        }

        private async Task WriteAuditEventAsync(
            string appId,
            string moduleId,
            string? actionId,
            string operation,
            string actorUserId,
            string actorRole,
            string correlationId,
            string path,
            long requestBytes,
            long responseBytes,
            bool succeeded,
            int statusCode,
            string? errorMessage,
            CancellationToken cancellationToken)
        {
            try
            {
                var evt = new AppModuleProxyAuditEvent
                {
                    AppId = appId,
                    ModuleId = moduleId,
                    ActionId = actionId,
                    Operation = operation,
                    ActorUserId = actorUserId,
                    ActorRole = actorRole,
                    CorrelationId = correlationId,
                    Timestamp = DateTime.UtcNow,
                    Success = succeeded,
                    StatusCode = statusCode,
                    Path = path,
                    RequestBytes = requestBytes,
                    ResponseBytes = responseBytes,
                    ErrorMessage = errorMessage
                };

                await _audit.InsertAsync(evt, cancellationToken);
            }
            catch (Exception ex)
            {
                _logs.Warn("Apps.Modules.Audit.WriteFailed", new StructuredLogContext
                {
                    CorrelationId = correlationId,
                    UserId = actorUserId,
                    AppId = appId
                }, new { moduleId, actionId, operation, error = ex.Message });
            }
        }

        private string GetCurrentUserId()
        {
            return _userContextAccessor.GetUser(User)?.UserId ?? "unknown";
        }

        private bool IsPlatformAdmin()
        {
            var user = _userContextAccessor.GetUser(User);
            if (user is null)
            {
                return false;
            }

            return user.IsSuperAdmin
                   || user.Roles.Any(r => string.Equals(r, "Admin", StringComparison.OrdinalIgnoreCase));
        }

        private string GetOrCreateCorrelationId()
        {
            var header = Request.Headers["x-correlation-id"].ToString();
            return string.IsNullOrWhiteSpace(header) ? Guid.NewGuid().ToString() : header;
        }
    }
}
