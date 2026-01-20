using System.Diagnostics;
using System.Security.Claims;
using AuthServer.Api.DTOs;
using AuthServer.Api.Models;
using AuthServer.Api.Repository.Interfaces;
using AuthServer.Api.Services;
using AuthServer.Api.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Mozaiks.Auth;

namespace AuthServer.Api.Controllers
{
    [ApiController]
    [Route("api/apps/{appId}/admin-surface")]
    [Authorize]
    public sealed class AppAdminSurfaceController : ControllerBase
    {
        private readonly MozaiksAppService _apps;
        private readonly ITeamMembersRepository _teamMembers;
        private readonly IAppAdminSurfaceRepository _adminSurfaces;
        private readonly IDataProtector _protector;
        private readonly StructuredLogEmitter _logs;
        private readonly IUserContextAccessor _userContextAccessor;

        public AppAdminSurfaceController(
            MozaiksAppService apps,
            ITeamMembersRepository teamMembers,
            IAppAdminSurfaceRepository adminSurfaces,
            IDataProtectionProvider dataProtectionProvider,
            StructuredLogEmitter logs,
            IUserContextAccessor userContextAccessor)
        {
            _apps = apps;
            _teamMembers = teamMembers;
            _adminSurfaces = adminSurfaces;
            _protector = dataProtectionProvider.CreateProtector("Mozaiks.AppAdminSurface.AdminKey.v1");
            _logs = logs;
            _userContextAccessor = userContextAccessor;
        }

        [HttpPut]
        public async Task<IActionResult> Configure(string appId, [FromBody] ConfigureAdminSurfaceRequest request, CancellationToken cancellationToken)
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
            _logs.Info("Apps.AdminSurface.Configure.Requested", context, new { request?.BaseUrl });

            if (!IsPlatformAdmin())
            {
                _logs.Warn("Apps.AdminSurface.Configure.Forbidden", context);
                return Forbid();
            }

            if (!TryNormalizeBaseUrl(request?.BaseUrl, out var baseUrl, out var baseUrlError))
            {
                return BadRequest(new { error = "bad_request", message = baseUrlError });
            }

            var adminKey = (request?.AdminKey ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(adminKey))
            {
                return BadRequest(new { error = "bad_request", message = "adminKey is required" });
            }

            if (adminKey.Length < 16)
            {
                return BadRequest(new { error = "bad_request", message = "adminKey must be at least 16 characters" });
            }

            if (string.IsNullOrWhiteSpace(appId))
            {
                return BadRequest(new { error = "InvalidAppId" });
            }

            var app = await _apps.GetByIdAsync(appId);
            if (app is null || app.IsDeleted || app.Status == AppStatus.Deleted)
            {
                return NotFound(new { error = "NotFound" });
            }

            var protectedKey = _protector.Protect(adminKey);
            var existing = await _adminSurfaces.GetByAppIdAsync(appId, cancellationToken);

            var model = new AppAdminSurfaceModel
            {
                AppId = appId,
                BaseUrl = baseUrl,
                AdminKeyProtected = protectedKey,
                KeyVersion = existing?.KeyVersion + 1 ?? 1,
                LastRotatedAt = DateTime.UtcNow,
                Notes = string.IsNullOrWhiteSpace(request?.Notes) ? null : request!.Notes!.Trim(),
                UpdatedByUserId = userId
            };

            await _adminSurfaces.UpsertAsync(model, cancellationToken);

            var response = new ConfigureAdminSurfaceResponse
            {
                Ok = true,
                AppId = appId,
                BaseUrl = baseUrl,
                Configured = true,
                UpdatedAt = model.UpdatedAt
            };

            _logs.Info("Apps.AdminSurface.Configure.Completed", context, new { response.BaseUrl, response.UpdatedAt });

            return Ok(response);
        }

        [HttpGet("status")]
        public async Task<IActionResult> GetStatus(string appId, CancellationToken cancellationToken)
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
            _logs.Info("Apps.AdminSurface.Status.Requested", context);

            if (string.IsNullOrWhiteSpace(appId))
            {
                return BadRequest(new { error = "InvalidAppId" });
            }

            var app = await _apps.GetByIdAsync(appId);
            if (app is null || app.IsDeleted || app.Status == AppStatus.Deleted)
            {
                return NotFound(new { error = "NotFound" });
            }

            if (!IsPlatformAdmin() && !string.Equals(app.OwnerUserId, userId, StringComparison.OrdinalIgnoreCase))
            {
                var member = await _teamMembers.GetByAppAndUserIdAsync(appId, userId);
                if (member is null)
                {
                    _logs.Warn("Apps.AdminSurface.Status.Forbidden", context);
                    return Forbid();
                }
            }

            var existing = await _adminSurfaces.GetByAppIdAsync(appId, cancellationToken);
            var configured = existing is not null &&
                             !string.IsNullOrWhiteSpace(existing.BaseUrl) &&
                             !string.IsNullOrWhiteSpace(existing.AdminKeyProtected);

            var response = new AdminSurfaceStatusResponse
            {
                AppId = appId,
                Configured = configured,
                BaseUrl = configured ? existing!.BaseUrl : null,
                UpdatedAt = configured ? existing!.UpdatedAt : null
            };

            _logs.Info("Apps.AdminSurface.Status.Completed", context, new { response.Configured, response.BaseUrl });

            return Ok(response);
        }

        private static bool TryNormalizeBaseUrl(string? input, out string normalized, out string error)
        {
            normalized = string.Empty;
            error = string.Empty;

            var value = (input ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                error = "baseUrl is required";
                return false;
            }

            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            {
                error = "baseUrl must be a valid absolute URL";
                return false;
            }

            if (!string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(uri.Scheme, "http", StringComparison.OrdinalIgnoreCase))
            {
                error = "baseUrl must be http or https";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(uri.UserInfo))
            {
                error = "baseUrl must not include embedded credentials";
                return false;
            }

            normalized = uri.ToString().TrimEnd('/');
            return true;
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
