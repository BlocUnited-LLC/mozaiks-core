using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    [Route("api/apps/{appId}/admin/users")]
    [Authorize]
    public sealed class AppAdminUsersProxyController : ControllerBase
    {
        private readonly MozaiksAppService _apps;
        private readonly ITeamMembersRepository _teamMembers;
        private readonly IAppModuleProxyService _proxy;
        private readonly StructuredLogEmitter _logs;
        private readonly IUserContextAccessor _userContextAccessor;

        public AppAdminUsersProxyController(
            MozaiksAppService apps,
            ITeamMembersRepository teamMembers,
            IAppModuleProxyService proxy,
            StructuredLogEmitter logs,
            IUserContextAccessor userContextAccessor)
        {
            _apps = apps;
            _teamMembers = teamMembers;
            _proxy = proxy;
            _logs = logs;
            _userContextAccessor = userContextAccessor;
        }

        [HttpGet]
        public async Task<IActionResult> ListUsers(
            string appId,
            [FromQuery] int? page,
            [FromQuery] int? limit,
            [FromQuery] string? q,
            [FromQuery] bool? disabled,
            CancellationToken cancellationToken)
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
            _logs.Info("Apps.AdminUsers.List.Requested", context, new { page, limit, q, disabled });

            var access = await AuthorizeOwnerOrAdminAsync(appId, userId, correlationId, cancellationToken);
            if (access.Result is not null)
            {
                return access.Result;
            }

            var path = BuildUsersListPath(page, limit, q, disabled);
            var result = await _proxy.SendAsync(appId, path, HttpMethod.Get, null, correlationId, cancellationToken);

            _logs.Info("Apps.AdminUsers.List.Completed", context, new { succeeded = result.Succeeded, upstreamStatus = result.UpstreamStatusCode });

            // Contract guarantee: return an envelope shape when upstream is a bare array.
            // Preferred: { items, page, limit, total, pages }
            // Tolerated: { users, page, pageSize, totalCount, totalPages }
            if (result.Succeeded && result.Body is not null)
            {
                var upstreamStatusCode = result.UpstreamStatusCode ?? 200;
                if (upstreamStatusCode == 204 || result.Body.Length == 0)
                {
                    return StatusCode(upstreamStatusCode);
                }

                var contentType = string.IsNullOrWhiteSpace(result.ContentType) ? "application/json" : result.ContentType!;
                if (contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
                {
                    var adapted = TryAdaptUsersListEnvelope(result.Body, page, limit);
                    if (adapted is not null)
                    {
                        return new ContentResult
                        {
                            StatusCode = upstreamStatusCode,
                            ContentType = "application/json",
                            Content = Encoding.UTF8.GetString(adapted)
                        };
                    }
                }

                // Pass-through if we couldn't adapt safely.
                return new ContentResult
                {
                    StatusCode = upstreamStatusCode,
                    ContentType = contentType,
                    Content = Encoding.UTF8.GetString(result.Body)
                };
            }

            return ToProxyActionResult(result, correlationId);
        }

        [HttpGet("{userId}")]
        public async Task<IActionResult> GetUser(
            string appId,
            string userId,
            CancellationToken cancellationToken)
        {
            var actorUserId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(actorUserId) || actorUserId == "unknown")
            {
                return Unauthorized(new { error = "Unauthorized", reason = "MissingUserId" });
            }

            if (string.IsNullOrWhiteSpace(userId))
            {
                return BadRequest(new { error = "bad_request", message = "userId is required" });
            }

            var correlationId = GetOrCreateCorrelationId();
            Response.Headers["x-correlation-id"] = correlationId;

            Activity.Current?.SetTag("correlationId", correlationId);
            Activity.Current?.SetTag("userId", actorUserId);
            Activity.Current?.SetTag("appId", appId);

            var context = new StructuredLogContext { CorrelationId = correlationId, UserId = actorUserId, AppId = appId };
            _logs.Info("Apps.AdminUsers.Get.Requested", context, new { targetUserId = userId });

            var access = await AuthorizeOwnerOrAdminAsync(appId, actorUserId, correlationId, cancellationToken);
            if (access.Result is not null)
            {
                return access.Result;
            }

            var path = $"/__mozaiks/admin/users/{Uri.EscapeDataString(userId)}";
            var result = await _proxy.SendAsync(appId, path, HttpMethod.Get, null, correlationId, cancellationToken);

            _logs.Info("Apps.AdminUsers.Get.Completed", context, new { succeeded = result.Succeeded, upstreamStatus = result.UpstreamStatusCode });
            return ToProxyActionResult(result, correlationId);
        }

        [HttpPost("action")]
        public async Task<IActionResult> InvokeAction(
            string appId,
            [FromBody] JsonElement requestBody,
            CancellationToken cancellationToken)
        {
            var actorUserId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(actorUserId) || actorUserId == "unknown")
            {
                return Unauthorized(new { error = "Unauthorized", reason = "MissingUserId" });
            }

            var correlationId = GetOrCreateCorrelationId();
            Response.Headers["x-correlation-id"] = correlationId;

            Activity.Current?.SetTag("correlationId", correlationId);
            Activity.Current?.SetTag("userId", actorUserId);
            Activity.Current?.SetTag("appId", appId);

            var context = new StructuredLogContext { CorrelationId = correlationId, UserId = actorUserId, AppId = appId };
            _logs.Info("Apps.AdminUsers.Action.Requested", context);

            var access = await AuthorizeOwnerOrAdminAsync(appId, actorUserId, correlationId, cancellationToken);
            if (access.Result is not null)
            {
                return access.Result;
            }

            var jsonBytes = NormalizeAdminUsersActionRequest(requestBody);
            var path = "/__mozaiks/admin/users/action";
            var result = await _proxy.SendAsync(appId, path, HttpMethod.Post, jsonBytes, correlationId, cancellationToken);

            _logs.Info("Apps.AdminUsers.Action.Completed", context, new { succeeded = result.Succeeded, upstreamStatus = result.UpstreamStatusCode });
            return ToProxyActionResult(result, correlationId);
        }

        private sealed class AppAccessResult
        {
            public IActionResult? Result { get; init; }
            public string ActorRole { get; init; } = "team";
        }

        private sealed class ProxyErrorResponse
        {
            [JsonPropertyName("error")]
            public string Error { get; init; } = "app_runtime_proxy_failed";

            [JsonPropertyName("message")]
            public string Message { get; init; } = "Upstream request failed.";

            [JsonPropertyName("correlationId")]
            public string CorrelationId { get; init; } = string.Empty;

            [JsonPropertyName("upstreamStatusCode")]
            [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
            public int? UpstreamStatusCode { get; init; }
        }

        private async Task<AppAccessResult> AuthorizeOwnerOrAdminAsync(
            string appId,
            string userId,
            string correlationId,
            CancellationToken cancellationToken)
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
                return new AppAccessResult { ActorRole = "platform_admin" };
            }

            if (string.Equals(app.OwnerUserId, userId, StringComparison.OrdinalIgnoreCase))
            {
                return new AppAccessResult { ActorRole = "owner" };
            }

            // App-level admin check.
            var member = await _teamMembers.GetByAppAndUserIdAsync(appId, userId);
            if (member is not null && member.MemberStatus == 1)
            {
                var role = (member.Role ?? string.Empty).Trim();
                if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(role, "Owner", StringComparison.OrdinalIgnoreCase))
                {
                    return new AppAccessResult { ActorRole = "app_admin" };
                }
            }

            return new AppAccessResult { Result = Forbid() };
        }

        private static string BuildUsersListPath(int? page, int? limit, string? q, bool? disabled)
        {
            var pairs = new List<string>();

            if (page is not null)
            {
                pairs.Add("page=" + Uri.EscapeDataString(page.Value.ToString()));
            }

            if (limit is not null)
            {
                pairs.Add("limit=" + Uri.EscapeDataString(limit.Value.ToString()));
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                pairs.Add("q=" + Uri.EscapeDataString(q));
            }

            if (disabled is not null)
            {
                pairs.Add("disabled=" + Uri.EscapeDataString(disabled.Value ? "true" : "false"));
            }

            return pairs.Count == 0
                ? "/__mozaiks/admin/users"
                : "/__mozaiks/admin/users?" + string.Join("&", pairs);
        }

        private static byte[] NormalizeAdminUsersActionRequest(JsonElement requestBody)
        {
            // UI sends { action, actionId, targetIds, params }.
            // Backend treats action and actionId as aliases for compatibility.
            try
            {
                var node = JsonNode.Parse(requestBody.GetRawText());
                if (node is not JsonObject obj)
                {
                    return Encoding.UTF8.GetBytes(requestBody.GetRawText());
                }

                var action = obj["action"]?.GetValue<string?>();
                var actionId = obj["actionId"]?.GetValue<string?>();

                if (string.IsNullOrWhiteSpace(action) && !string.IsNullOrWhiteSpace(actionId))
                {
                    obj["action"] = actionId;
                }

                if (string.IsNullOrWhiteSpace(actionId) && !string.IsNullOrWhiteSpace(action))
                {
                    obj["actionId"] = action;
                }

                return JsonSerializer.SerializeToUtf8Bytes(obj);
            }
            catch
            {
                return Encoding.UTF8.GetBytes(requestBody.GetRawText());
            }
        }

        private static byte[]? TryAdaptUsersListEnvelope(byte[] upstreamBody, int? page, int? limit)
        {
            try
            {
                using var doc = JsonDocument.Parse(upstreamBody);
                var root = doc.RootElement;

                if (root.ValueKind == JsonValueKind.Object)
                {
                    // Already in a supported envelope shape? Leave as-is.
                    if (IsPreferredEnvelope(root) || IsToleratedEnvelope(root))
                    {
                        return upstreamBody;
                    }

                    // If upstream provided items/users array but omitted meta, fill meta.
                    if (TryGetArrayProperty(root, "items", out var itemsArray))
                    {
                        return BuildPreferredEnvelope(itemsArray, page, limit);
                    }

                    if (TryGetArrayProperty(root, "users", out var usersArray))
                    {
                        // Convert tolerated-ish payload into preferred envelope for consistency.
                        return BuildPreferredEnvelope(usersArray, page, limit);
                    }

                    return null;
                }

                if (root.ValueKind == JsonValueKind.Array)
                {
                    return BuildPreferredEnvelope(root, page, limit);
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static bool IsPreferredEnvelope(JsonElement obj)
        {
            return obj.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array
                   && obj.TryGetProperty("page", out var page) && page.ValueKind is JsonValueKind.Number
                   && obj.TryGetProperty("limit", out var limit) && limit.ValueKind is JsonValueKind.Number
                   && obj.TryGetProperty("total", out var total) && total.ValueKind is JsonValueKind.Number
                   && obj.TryGetProperty("pages", out var pages) && pages.ValueKind is JsonValueKind.Number;
        }

        private static bool IsToleratedEnvelope(JsonElement obj)
        {
            return obj.TryGetProperty("users", out var users) && users.ValueKind == JsonValueKind.Array
                   && obj.TryGetProperty("page", out var page) && page.ValueKind is JsonValueKind.Number
                   && obj.TryGetProperty("pageSize", out var pageSize) && pageSize.ValueKind is JsonValueKind.Number
                   && obj.TryGetProperty("totalCount", out var totalCount) && totalCount.ValueKind is JsonValueKind.Number
                   && obj.TryGetProperty("totalPages", out var totalPages) && totalPages.ValueKind is JsonValueKind.Number;
        }

        private static bool TryGetArrayProperty(JsonElement obj, string prop, out JsonElement array)
        {
            array = default;
            return obj.TryGetProperty(prop, out array) && array.ValueKind == JsonValueKind.Array;
        }

        private static byte[] BuildPreferredEnvelope(JsonElement itemsArray, int? page, int? limit)
        {
            var pageValue = page.GetValueOrDefault(1);
            if (pageValue <= 0) pageValue = 1;

            var limitValue = limit.GetValueOrDefault(itemsArray.GetArrayLength());
            if (limitValue <= 0) limitValue = itemsArray.GetArrayLength();

            var total = itemsArray.GetArrayLength();
            var pages = limitValue > 0 ? (int)Math.Ceiling((double)total / limitValue) : 1;
            if (pages <= 0) pages = 1;

            var payload = new
            {
                items = JsonSerializer.Deserialize<object>(itemsArray.GetRawText()),
                page = pageValue,
                limit = limitValue,
                total,
                pages
            };

            return JsonSerializer.SerializeToUtf8Bytes(payload);
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

            var payload = new ProxyErrorResponse
            {
                Error = result.ErrorCode ?? "app_runtime_proxy_failed",
                Message = result.ErrorMessage ?? "Upstream request failed.",
                CorrelationId = correlationId,
                UpstreamStatusCode = result.UpstreamStatusCode
            };

            return StatusCode(statusCode, payload);
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
