using System.Diagnostics;
using System.Security.Claims;
using AuthServer.Api.DTOs;
using AuthServer.Api.Repository.Interfaces;
using AuthServer.Api.Services;
using AuthServer.Api.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Mozaiks.Auth;

namespace AuthServer.Api.Controllers
{
    [Route("api/apps/{appId}/api-key")]
    [ApiController]
    [Authorize]
    public sealed class ApiKeyController : ControllerBase
    {
        private readonly MozaiksAppService _apps;
        private readonly ITeamMembersRepository _teamMembers;
        private readonly ApiKeyOptions _options;
        private readonly StructuredLogEmitter _logs;
        private readonly IUserContextAccessor _userContextAccessor;

        public ApiKeyController(
            MozaiksAppService apps,
            ITeamMembersRepository teamMembers,
            IOptions<ApiKeyOptions> options,
            StructuredLogEmitter logs,
            IUserContextAccessor userContextAccessor)
        {
            _apps = apps;
            _teamMembers = teamMembers;
            _options = options.Value;
            _logs = logs;
            _userContextAccessor = userContextAccessor;
        }

        [HttpPost("generate")]
        public async Task<IActionResult> Generate(string appId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(appId))
            {
                return BadRequest(new { error = "InvalidAppId" });
            }

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

            var context = new StructuredLogContext
            {
                CorrelationId = correlationId,
                UserId = userId,
                AppId = appId
            };

            _logs.Info("Apps.ApiKey.Generate.Requested", context);

            var app = await _apps.GetByIdAsync(appId);
            if (app is null)
            {
                _logs.Warn("Apps.ApiKey.Generate.AppNotFound", context);
                return NotFound(new { error = "NotFound" });
            }

            if (!IsPlatformAdmin() && !string.Equals(app.OwnerUserId, userId, StringComparison.OrdinalIgnoreCase))
            {
                _logs.Warn("Apps.ApiKey.Generate.Forbidden", context);
                return Forbid();
            }

            if (!string.IsNullOrWhiteSpace(app.ApiKeyHash))
            {
                return Conflict(new
                {
                    error = "API key already exists. Use the regenerate endpoint to create a new key."
                });
            }

            var material = ApiKeyCrypto.Generate(_options);
            var createdAt = DateTime.UtcNow;

            var updated = await _apps.TryGenerateApiKeyAsync(appId, material.Hash, material.Prefix, createdAt);
            if (!updated)
            {
                return Conflict(new
                {
                    error = "API key already exists. Use the regenerate endpoint to create a new key."
                });
            }

            _logs.Info("Apps.ApiKey.Generate.Completed", context, new
            {
                prefix = material.Prefix,
                version = 1
            });

            return CreatedAtAction(
                nameof(GetStatus),
                new { appId },
                new ApiKeyGenerateResponse(material.ApiKey, createdAt, material.Prefix));
        }

        [HttpPost("regenerate")]
        public async Task<ActionResult<ApiKeyRegenerateResponse>> Regenerate(string appId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(appId))
            {
                return BadRequest(new { error = "InvalidAppId" });
            }

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

            var context = new StructuredLogContext
            {
                CorrelationId = correlationId,
                UserId = userId,
                AppId = appId
            };

            _logs.Info("Apps.ApiKey.Regenerate.Requested", context);

            var app = await _apps.GetByIdAsync(appId);
            if (app is null)
            {
                _logs.Warn("Apps.ApiKey.Regenerate.AppNotFound", context);
                return NotFound(new { error = "NotFound" });
            }

            if (!IsPlatformAdmin() && !string.Equals(app.OwnerUserId, userId, StringComparison.OrdinalIgnoreCase))
            {
                _logs.Warn("Apps.ApiKey.Regenerate.Forbidden", context);
                return Forbid();
            }

            var previousKeyInvalidated = !string.IsNullOrWhiteSpace(app.ApiKeyHash);
            var material = ApiKeyCrypto.Generate(_options);
            var createdAt = DateTime.UtcNow;

            var updated = await _apps.RegenerateApiKeyAsync(appId, material.Hash, material.Prefix, createdAt);
            if (!updated)
            {
                return NotFound(new { error = "NotFound" });
            }

            var version = app.ApiKeyVersion + 1;

            _logs.Info("Apps.ApiKey.Regenerate.Completed", context, new
            {
                prefix = material.Prefix,
                version,
                previousKeyInvalidated
            });

            return Ok(new ApiKeyRegenerateResponse(material.ApiKey, createdAt, material.Prefix, previousKeyInvalidated, version));
        }

        [HttpGet("status")]
        public async Task<IActionResult> GetStatus(string appId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(appId))
            {
                return BadRequest(new { error = "InvalidAppId" });
            }

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

            var context = new StructuredLogContext
            {
                CorrelationId = correlationId,
                UserId = userId,
                AppId = appId
            };

            _logs.Info("Apps.ApiKey.Status.Requested", context);

            var app = await _apps.GetByIdAsync(appId);
            if (app is null)
            {
                _logs.Warn("Apps.ApiKey.Status.AppNotFound", context);
                return NotFound(new { error = "NotFound" });
            }

            var isPlatformAdmin = IsPlatformAdmin();
            var isOwner = string.Equals(app.OwnerUserId, userId, StringComparison.OrdinalIgnoreCase);
            var isMember = isOwner;
            if (!isMember && !isPlatformAdmin)
            {
                var member = await _teamMembers.GetByAppAndUserIdAsync(appId, userId);
                isMember = member is not null;
            }

            if (!isMember && !isPlatformAdmin)
            {
                _logs.Warn("Apps.ApiKey.Status.Forbidden", context);
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(app.ApiKeyHash))
            {
                return Ok(new { exists = false });
            }

            var response = new ApiKeyStatusResponse(
                Exists: true,
                Prefix: app.ApiKeyPrefix ?? string.Empty,
                CreatedAt: app.ApiKeyCreatedAt,
                LastUsedAt: app.ApiKeyLastUsedAt,
                Version: app.ApiKeyVersion);

            _logs.Info("Apps.ApiKey.Status.Completed", context, new { exists = true, prefix = response.Prefix, version = response.Version });

            return Ok(response);
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
