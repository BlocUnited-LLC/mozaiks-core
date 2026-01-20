using System.Diagnostics;
using AuthServer.Api.DTOs;
using AuthServer.Api.Models;
using AuthServer.Api.Services;
using AuthServer.Api.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mozaiks.Auth;

namespace AuthServer.Api.Controllers;

[ApiController]
[Route("api/internal/apps")]
[Authorize(Policy = MozaiksAuthDefaults.InternalServicePolicy)]
public sealed class InternalAppsController : ControllerBase
{
    private readonly MozaiksAppService _apps;
    private readonly StructuredLogEmitter _logs;
    private readonly IUserContextAccessor _userContextAccessor;

    public InternalAppsController(MozaiksAppService apps, StructuredLogEmitter logs, IUserContextAccessor userContextAccessor)
    {
        _apps = apps;
        _logs = logs;
        _userContextAccessor = userContextAccessor;
    }

    [HttpPatch("{appId}")]
    public async Task<IActionResult> Patch(string appId, [FromBody] UpdateMozaiksAppRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return BadRequest(new { error = "InvalidAppId" });
        }

        if (request is null)
        {
            return BadRequest(new { error = "InvalidRequest" });
        }

        var correlationId = GetOrCreateCorrelationId();
        Response.Headers["x-correlation-id"] = correlationId;

        var actorUserId = _userContextAccessor.GetUser(User)?.UserId ?? "internal";

        Activity.Current?.SetTag("correlationId", correlationId);
        Activity.Current?.SetTag("userId", actorUserId);
        Activity.Current?.SetTag("appId", appId);

        var context = new StructuredLogContext
        {
            CorrelationId = correlationId,
            UserId = actorUserId,
            AppId = appId
        };

        _logs.Info("InternalApps.Patch.Requested", context);

        var app = await _apps.GetByIdAsync(appId);
        if (app is null)
        {
            _logs.Warn("InternalApps.Patch.NotFound", context);
            return NotFound(new { error = "NotFound" });
        }

        if (app.IsDeleted || app.Status == AppStatus.Deleted)
        {
            _logs.Warn("InternalApps.Patch.Deleted", context);
            return Conflict(new { error = "invalid_state", message = "Cannot update a deleted app." });
        }

        if (request.Name != null || request.Description != null || request.LogoUrl != null || request.InstalledPlugins != null)
        {
            var patch = new AppConfigPatchRequest
            {
                DisplayName = request.Name,
                Description = request.Description,
                AvatarUrl = request.LogoUrl,
                InstalledPlugins = request.InstalledPlugins
            };

            await _apps.PatchAppConfigAsync(appId, patch);
        }

        if (!string.IsNullOrWhiteSpace(request.Visibility))
        {
            var publish = string.Equals(request.Visibility, "PUBLIC", StringComparison.OrdinalIgnoreCase);
            await _apps.SetPublishStatusAsync(appId, publish);
        }

        _logs.Info("InternalApps.Patch.Completed", context);

        return NoContent();
    }

    [HttpPatch("{appId}/feature-flags")]
    public async Task<IActionResult> ToggleFeatureFlag(string appId, [FromBody] FeatureFlagToggleRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return BadRequest(new { error = "InvalidAppId" });
        }

        if (request is null || string.IsNullOrWhiteSpace(request.Flag))
        {
            return BadRequest(new { error = "InvalidRequest" });
        }

        var correlationId = GetOrCreateCorrelationId();
        Response.Headers["x-correlation-id"] = correlationId;

        var actorUserId = _userContextAccessor.GetUser(User)?.UserId ?? "internal";

        Activity.Current?.SetTag("correlationId", correlationId);
        Activity.Current?.SetTag("userId", actorUserId);
        Activity.Current?.SetTag("appId", appId);

        var context = new StructuredLogContext
        {
            CorrelationId = correlationId,
            UserId = actorUserId,
            AppId = appId
        };

        _logs.Info("InternalApps.FeatureFlag.Requested", context, new { request.Flag, request.Enabled });

        var updated = await _apps.SetFeatureFlagAsync(appId, request.Flag, request.Enabled);
        if (!updated)
        {
            return NotFound(new { error = "NotFound" });
        }

        _logs.Info("InternalApps.FeatureFlag.Completed", context);

        return NoContent();
    }

    private string GetOrCreateCorrelationId()
    {
        var header = Request.Headers["x-correlation-id"].ToString();
        return string.IsNullOrWhiteSpace(header) ? Guid.NewGuid().ToString() : header;
    }
}

