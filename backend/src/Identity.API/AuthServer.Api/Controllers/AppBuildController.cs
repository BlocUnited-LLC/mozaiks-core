using System.Diagnostics;
using System.Security.Claims;
using AuthServer.Api.DTOs;
using AuthServer.Api.Models;
using AuthServer.Api.Repository.Interfaces;
using AuthServer.Api.Services;
using AuthServer.Api.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mozaiks.Auth;

namespace AuthServer.Api.Controllers;

[ApiController]
[Route("api/apps/{appId}")]
public sealed class AppBuildController : ControllerBase
{
    private static readonly HashSet<string> AllowedEventTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "build_started",
        "build_completed",
        "build_failed"
    };

    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "building",
        "built",
        "error"
    };

    private readonly MozaiksAppService _apps;
    private readonly IAppBuildStatusRepository _status;
    private readonly IAppBuildEventRepository _events;
    private readonly NotificationApiClient _notifications;
    private readonly StructuredLogEmitter _logs;
    private readonly IUserContextAccessor _userContextAccessor;

    public AppBuildController(
        MozaiksAppService apps,
        IAppBuildStatusRepository status,
        IAppBuildEventRepository events,
        NotificationApiClient notifications,
        StructuredLogEmitter logs,
        IUserContextAccessor userContextAccessor)
    {
        _apps = apps;
        _status = status;
        _events = events;
        _notifications = notifications;
        _logs = logs;
        _userContextAccessor = userContextAccessor;
    }

    [HttpPost("build-events")]
    [Authorize(Policy = MozaiksAuthDefaults.InternalServicePolicy)]
    public async Task<IActionResult> PostBuildEvent(
        string appId,
        [FromBody] AppBuildEventRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return BadRequest(new { error = "InvalidAppId" });
        }

        var correlationId = GetOrCreateCorrelationId();
        Response.Headers["x-correlation-id"] = correlationId;

        Activity.Current?.SetTag("correlationId", correlationId);
        Activity.Current?.SetTag("appId", appId);

        const bool isInternalCall = true;
        var actorUserId = "internal";

        Activity.Current?.SetTag("userId", actorUserId);

        var context = new StructuredLogContext
        {
            CorrelationId = correlationId,
            UserId = actorUserId,
            AppId = appId
        };

        if (!string.IsNullOrWhiteSpace(request.AppId) && !string.Equals(request.AppId, appId, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "AppIdMismatch", correlationId });
        }

        var eventType = (request.EventType ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(eventType) || !AllowedEventTypes.Contains(eventType))
        {
            return BadRequest(new { error = "InvalidEventType", allowed = AllowedEventTypes, correlationId });
        }

        var buildId = (request.BuildId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(buildId))
        {
            return BadRequest(new { error = "InvalidBuildId", correlationId });
        }

        var app = await _apps.GetByIdAsync(appId);
        if (app is null || app.IsDeleted || app.Status == AppStatus.Deleted)
        {
            return NotFound(new { error = "NotFound", correlationId });
        }

        var status = (request.Status ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(status))
        {
            status = eventType.ToLowerInvariant() switch
            {
                "build_started" => "building",
                "build_completed" => "built",
                "build_failed" => "error",
                _ => "building"
            };
        }

        if (!AllowedStatuses.Contains(status))
        {
            return BadRequest(new { error = "InvalidStatus", allowed = AllowedStatuses, correlationId });
        }

        var existing = await _status.GetByAppIdAsync(appId, cancellationToken);

        var now = DateTime.UtcNow;
        var startedAtUtc = existing?.StartedAtUtc;
        if (string.Equals(eventType, "build_started", StringComparison.OrdinalIgnoreCase))
        {
            startedAtUtc = now;
        }
        else if (existing is null || !string.Equals(existing.BuildId, buildId, StringComparison.OrdinalIgnoreCase))
        {
            startedAtUtc ??= now;
        }

        DateTime? completedAtUtc = existing?.CompletedAtUtc;
        if (string.Equals(eventType, "build_completed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(eventType, "build_failed", StringComparison.OrdinalIgnoreCase))
        {
            completedAtUtc = (request.CompletedAt?.UtcDateTime) ?? now;
        }

        var artifacts = new AppBuildArtifactsModel
        {
            PreviewUrl = request.Artifacts?.PreviewUrl ?? existing?.Artifacts?.PreviewUrl,
            ExportDownloadUrl = request.Artifacts?.ExportDownloadUrl ?? existing?.Artifacts?.ExportDownloadUrl
        };

        AppBuildErrorModel? error = null;
        if (string.Equals(status, "error", StringComparison.OrdinalIgnoreCase))
        {
            var errorMessage = (request.Error?.Message ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(errorMessage))
            {
                errorMessage = "Build failed";
            }

            error = new AppBuildErrorModel
            {
                Message = errorMessage,
                Details = string.IsNullOrWhiteSpace(request.Error?.Details) ? null : request.Error!.Details
            };
        }

        var statusModel = new AppBuildStatusModel
        {
            AppId = appId,
            BuildId = buildId,
            EventType = eventType,
            Status = status,
            StartedAtUtc = startedAtUtc,
            CompletedAtUtc = completedAtUtc,
            Artifacts = artifacts,
            Error = error
        };

        await _status.UpsertAsync(statusModel, cancellationToken);

        await _events.InsertAsync(new AppBuildEventModel
        {
            AppId = appId,
            BuildId = buildId,
            EventType = eventType,
            Status = status,
            OccurredAtUtc = now,
            Artifacts = request.Artifacts == null
                ? null
                : new AppBuildArtifactsModel
                {
                    PreviewUrl = request.Artifacts.PreviewUrl,
                    ExportDownloadUrl = request.Artifacts.ExportDownloadUrl
                },
            Error = request.Error == null
                ? null
                : new AppBuildErrorModel
                {
                    Message = request.Error.Message ?? string.Empty,
                    Details = request.Error.Details
                }
        }, cancellationToken);

        _logs.Info("Apps.BuildEvents.Received", context, new { internalCall = isInternalCall, eventType, status, buildId });

        if (string.Equals(eventType, "build_completed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(eventType, "build_failed", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                await _notifications.SendAppBuildEventAsync(
                    appId: appId,
                    recipientUserId: app.OwnerUserId,
                    appName: app.Name,
                    buildId: buildId,
                    status: string.Equals(status, "built", StringComparison.OrdinalIgnoreCase) ? "built" : "error",
                    correlationId: correlationId,
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                _logs.Warn("Apps.BuildEvents.NotifyFailed", context, new { error = ex.Message });
            }
        }

        return Ok(ToResponse(statusModel));
    }

    [HttpGet("build-latest")]
    [Authorize]
    public async Task<ActionResult<AppBuildLatestResponse>> GetBuildLatest(string appId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return BadRequest(new { error = "InvalidAppId" });
        }

        var correlationId = GetOrCreateCorrelationId();
        Response.Headers["x-correlation-id"] = correlationId;

        var actorUserId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(actorUserId) || actorUserId == "unknown")
        {
            return Unauthorized(new { error = "Unauthorized", reason = "MissingUserId", correlationId });
        }

        var app = await _apps.GetByIdAsync(appId);
        if (app is null || app.IsDeleted || app.Status == AppStatus.Deleted)
        {
            return NotFound(new { error = "NotFound", correlationId });
        }

        if (!IsPlatformAdmin() && !string.Equals(app.OwnerUserId, actorUserId, StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        var latest = await _status.GetByAppIdAsync(appId, cancellationToken);
        if (latest is null)
        {
            return Ok(new AppBuildLatestResponse(
                AppId: appId,
                BuildId: null,
                Status: "unknown",
                StartedAtUtc: null,
                CompletedAtUtc: null,
                Artifacts: new AppBuildArtifactsDto(),
                Error: null,
                UpdatedAtUtc: DateTimeOffset.UtcNow));
        }

        return Ok(ToResponse(latest));
    }

    private static AppBuildLatestResponse ToResponse(AppBuildStatusModel latest)
    {
        return new AppBuildLatestResponse(
            AppId: latest.AppId,
            BuildId: latest.BuildId,
            Status: latest.Status,
            StartedAtUtc: latest.StartedAtUtc.HasValue ? new DateTimeOffset(latest.StartedAtUtc.Value, TimeSpan.Zero) : null,
            CompletedAtUtc: latest.CompletedAtUtc.HasValue ? new DateTimeOffset(latest.CompletedAtUtc.Value, TimeSpan.Zero) : null,
            Artifacts: new AppBuildArtifactsDto
            {
                PreviewUrl = latest.Artifacts?.PreviewUrl,
                ExportDownloadUrl = latest.Artifacts?.ExportDownloadUrl
            },
            Error: latest.Error == null
                ? null
                : new AppBuildErrorDto
                {
                    Message = latest.Error.Message,
                    Details = latest.Error.Details
                },
            UpdatedAtUtc: new DateTimeOffset(latest.UpdatedAt, TimeSpan.Zero));
    }

    private string GetCurrentUserId()
        => _userContextAccessor.GetUser(User)?.UserId ?? "unknown";

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
