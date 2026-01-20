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
[Route("deploy")]
[Authorize]
public sealed class DeployController : ControllerBase
{
    private readonly MozaiksAppService _apps;
    private readonly IAppBuildStatusRepository _builds;
    private readonly AppEntitlementsService _entitlements;
    private readonly IDeploymentService _deployment;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly StructuredLogEmitter _logs;
    private readonly IUserContextAccessor _userContextAccessor;

    public DeployController(
        MozaiksAppService apps,
        IAppBuildStatusRepository builds,
        AppEntitlementsService entitlements,
        IDeploymentService deployment,
        IHttpClientFactory httpClientFactory,
        StructuredLogEmitter logs,
        IUserContextAccessor userContextAccessor)
    {
        _apps = apps;
        _builds = builds;
        _entitlements = entitlements;
        _deployment = deployment;
        _httpClientFactory = httpClientFactory;
        _logs = logs;
        _userContextAccessor = userContextAccessor;
    }

    /// <summary>
    /// Export the generated repo bundle as a zip file.
    /// Frontend expects: POST /deploy/export-repo (blob response).
    /// </summary>
    [HttpPost("export-repo")]
    public async Task<IActionResult> ExportRepo([FromBody] DeployActionRequest request, CancellationToken cancellationToken)
    {
        var correlationId = GetOrCreateCorrelationId();
        Response.Headers["x-correlation-id"] = correlationId;

        var appId = (request.AppId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(appId))
        {
            return BadRequest(new { error = "InvalidAppId", correlationId });
        }

        var userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId) || userId == "unknown")
        {
            return Unauthorized(new { error = "Unauthorized", reason = "MissingUserId", correlationId });
        }

        Activity.Current?.SetTag("correlationId", correlationId);
        Activity.Current?.SetTag("userId", userId);
        Activity.Current?.SetTag("appId", appId);

        var context = new StructuredLogContext { CorrelationId = correlationId, UserId = userId, AppId = appId };

        var app = await _apps.GetByIdAsync(appId);
        if (app is null || app.IsDeleted || app.Status == AppStatus.Deleted)
        {
            return NotFound(new { error = "NotFound", correlationId });
        }

        if (!IsPlatformAdmin() && !string.Equals(app.OwnerUserId, userId, StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        var latest = await _builds.GetByAppIdAsync(appId, cancellationToken);
        if (latest is null || !string.Equals(latest.Status, "built", StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(new { error = "build_not_completed", message = "Build must complete before export.", correlationId });
        }

        if (!string.IsNullOrWhiteSpace(request.BuildId) &&
            !string.Equals(request.BuildId.Trim(), latest.BuildId, StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(new { error = "build_mismatch", message = "Requested buildId does not match latest build.", correlationId });
        }

        AppEntitlements entitlements;
        try
        {
            entitlements = await _entitlements.GetForUserAndAppAsync(userId, appId, correlationId, cancellationToken);
        }
        catch
        {
            return StatusCode(503, new { error = "subscription_check_failed", message = "Unable to verify subscription status.", correlationId });
        }

        if (!entitlements.AllowExportRepo)
        {
            return StatusCode(403, new { error = "payment_required", message = "Export is gated by subscription.", correlationId });
        }

        var exportUrl = latest.Artifacts?.ExportDownloadUrl;
        if (string.IsNullOrWhiteSpace(exportUrl))
        {
            return Conflict(new { error = "missing_export_artifact", message = "Export artifact URL is not available for this build.", correlationId });
        }

        if (!Uri.TryCreate(exportUrl, UriKind.Absolute, out var exportUri) ||
            (exportUri.Scheme != Uri.UriSchemeHttps && exportUri.Scheme != Uri.UriSchemeHttp))
        {
            return StatusCode(500, new { error = "invalid_export_url", message = "Stored export URL is invalid.", correlationId });
        }

        _logs.Info("Deploy.ExportRepo.Requested", context, new { buildId = latest.BuildId });

        try
        {
            var client = _httpClientFactory.CreateClient("BuildArtifacts");
            using var response = await client.GetAsync(exportUri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logs.Warn("Deploy.ExportRepo.FetchFailed", context, new { statusCode = (int)response.StatusCode, body });
                return StatusCode(502, new { error = "export_fetch_failed", message = "Failed to fetch export bundle.", correlationId });
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            if (bytes.Length == 0)
            {
                return StatusCode(502, new { error = "export_empty", message = "Export bundle was empty.", correlationId });
            }

            var safeName = string.IsNullOrWhiteSpace(app.Name) ? "mozaiks-app" : app.Name.Trim();
            var fileName = $"{safeName}-build-{latest.BuildId}.zip";

            _logs.Info("Deploy.ExportRepo.Completed", context, new { buildId = latest.BuildId, sizeBytes = bytes.Length });

            return File(bytes, "application/zip", fileName);
        }
        catch (Exception ex)
        {
            _logs.Warn("Deploy.ExportRepo.Failed", context, new { error = ex.Message });
            return StatusCode(500, new { error = "export_failed", message = "Failed to export repository.", correlationId });
        }
    }

    /// <summary>
    /// Start a Mozaiks-hosted deploy (paywalled).
    /// Frontend expects: POST /deploy/mozaiks-hosted
    /// Body: { appId, buildId? }
    /// </summary>
    [HttpPost("mozaiks-hosted")]
    public async Task<IActionResult> DeployMozaiksHosted([FromBody] DeployActionRequest request, CancellationToken cancellationToken)
    {
        var correlationId = GetOrCreateCorrelationId();
        Response.Headers["x-correlation-id"] = correlationId;

        var appId = (request.AppId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(appId))
        {
            return BadRequest(new { error = "InvalidAppId", correlationId });
        }

        var userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId) || userId == "unknown")
        {
            return Unauthorized(new { error = "Unauthorized", reason = "MissingUserId", correlationId });
        }

        Activity.Current?.SetTag("correlationId", correlationId);
        Activity.Current?.SetTag("userId", userId);
        Activity.Current?.SetTag("appId", appId);

        var context = new StructuredLogContext { CorrelationId = correlationId, UserId = userId, AppId = appId };

        var app = await _apps.GetByIdAsync(appId);
        if (app is null || app.IsDeleted || app.Status == AppStatus.Deleted)
        {
            return NotFound(new { error = "NotFound", correlationId });
        }

        if (!IsPlatformAdmin() && !string.Equals(app.OwnerUserId, userId, StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        var latest = await _builds.GetByAppIdAsync(appId, cancellationToken);
        if (latest is null || !string.Equals(latest.Status, "built", StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(new { error = "build_not_completed", message = "Build must complete before deploy.", correlationId });
        }

        if (!string.IsNullOrWhiteSpace(request.BuildId) &&
            !string.Equals(request.BuildId.Trim(), latest.BuildId, StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(new { error = "build_mismatch", message = "Requested buildId does not match latest build.", correlationId });
        }

        AppEntitlements entitlements;
        try
        {
            entitlements = await _entitlements.GetForUserAndAppAsync(userId, appId, correlationId, cancellationToken);
        }
        catch
        {
            return StatusCode(503, new { error = "subscription_check_failed", message = "Unable to verify subscription status.", correlationId });
        }

        if (!entitlements.AllowHosting)
        {
            return StatusCode(403, new { error = "payment_required", message = "Hosting is not included in your plan.", correlationId });
        }

        var bundleUrl = latest.Artifacts?.ExportDownloadUrl;
        if (string.IsNullOrWhiteSpace(bundleUrl))
        {
            return Conflict(new { error = "missing_bundle", message = "Build bundle URL is not available.", correlationId });
        }

        _logs.Info("Deploy.MozaiksHosted.Requested", context, new { buildId = latest.BuildId });

        try
        {
            var deployRequest = new DeployAppRequest
            {
                BundleUrl = bundleUrl,
                RepoName = app.Name,
                Branch = "main",
                CommitMessage = "Deploy from Mozaiks (UI)"
            };

            var job = await _deployment.QueueDeploymentAsync(appId, userId, app.Name, correlationId, deployRequest, cancellationToken);

            _logs.Info("Deploy.MozaiksHosted.Queued", context, new { jobId = job.Id });

            return Accepted(new DeployAppAcceptedResponse(
                JobId: job.Id ?? string.Empty,
                Status: "queued",
                EstimatedCompletionSeconds: 60));
        }
        catch (Exception ex)
        {
            _logs.Warn("Deploy.MozaiksHosted.Failed", context, new { error = ex.Message });
            return BadRequest(new { error = "deploy_failed", message = ex.Message, correlationId });
        }
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
