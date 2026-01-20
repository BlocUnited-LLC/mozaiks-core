using System.Diagnostics;
using System.Security.Claims;
using AuthServer.Api.DTOs;
using AuthServer.Api.Services;
using AuthServer.Api.Shared;
using AuthServer.Api.Repository.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mozaiks.Auth;

namespace AuthServer.Api.Controllers
{
    [ApiController]
    [Route("api/apps/{appId}/deploy")]
    public sealed class AppDeploymentController : ControllerBase
    {
        private readonly IDeploymentService _deployment;
        private readonly MozaiksAppService _apps;
        private readonly IAppBuildStatusRepository _builds;
        private readonly AppEntitlementsService _entitlements;
        private readonly StructuredLogEmitter _logs;
        private readonly IUserContextAccessor _userContextAccessor;

        public AppDeploymentController(
            IDeploymentService deployment,
            MozaiksAppService apps,
            IAppBuildStatusRepository builds,
            AppEntitlementsService entitlements,
            StructuredLogEmitter logs,
            IUserContextAccessor userContextAccessor)
        {
            _deployment = deployment;
            _apps = apps;
            _builds = builds;
            _entitlements = entitlements;
            _logs = logs;
            _userContextAccessor = userContextAccessor;
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> DeployApp(string appId, [FromBody] DeployAppRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(appId))
            {
                return BadRequest(new { error = "InvalidAppId" });
            }

            var correlationId = GetOrCreateCorrelationId();
            Response.Headers["x-correlation-id"] = correlationId;

            Activity.Current?.SetTag("correlationId", correlationId);
            Activity.Current?.SetTag("appId", appId);

            var app = await _apps.GetByIdAsync(appId);
            if (app is null)
            {
                return NotFound(new { error = "NotFound" });
            }

            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId) || userId == "unknown")
            {
                return Unauthorized(new { error = "Unauthorized", reason = "MissingUserId" });
            }

            if (!IsPlatformAdmin() && !string.Equals(app.OwnerUserId, userId, StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            Activity.Current?.SetTag("userId", userId);

            var context = new StructuredLogContext
            {
                CorrelationId = correlationId,
                UserId = userId,
                AppId = appId
            };

            if (!IsPlatformAdmin())
            {
                var latest = await _builds.GetByAppIdAsync(appId, cancellationToken);
                if (latest is null || !string.Equals(latest.Status, "built", StringComparison.OrdinalIgnoreCase))
                {
                    _logs.Warn("Deploy.BuildNotCompleted", context, new { buildId = latest?.BuildId, status = latest?.Status });
                    return Conflict(new { error = "build_not_completed", message = "Build must complete before deploy.", correlationId });
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
            }

            _logs.Info("Deploy.Requested", context, new
            {
                internalCall = false,
                repoName = request.RepoName,
                branch = request.Branch
            });

            try
            {
                var job = await _deployment.QueueDeploymentAsync(appId, userId, app.Name, correlationId, request, cancellationToken);

                _logs.Info("Deploy.Queued", context, new { jobId = job.Id });

                return Accepted(new DeployAppAcceptedResponse(
                    JobId: job.Id ?? string.Empty,
                    Status: "queued",
                    EstimatedCompletionSeconds: 60));
            }
            catch (Exception ex)
            {
                _logs.Error("Deploy.QueueFailed", context, new { error = ex.Message, type = ex.GetType().Name });
                return BadRequest(new { error = "InvalidRequest", message = ex.Message });
            }
        }

        [HttpPost("/api/internal/apps/{appId}/deploy")]
        [Authorize(Policy = MozaiksAuthDefaults.InternalServicePolicy)]
        public async Task<IActionResult> DeployAppInternal(string appId, [FromBody] DeployAppRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(appId))
            {
                return BadRequest(new { error = "InvalidAppId" });
            }

            var correlationId = GetOrCreateCorrelationId();
            Response.Headers["x-correlation-id"] = correlationId;

            Activity.Current?.SetTag("correlationId", correlationId);
            Activity.Current?.SetTag("appId", appId);

            var app = await _apps.GetByIdAsync(appId);
            if (app is null)
            {
                return NotFound(new { error = "NotFound" });
            }

            var userId = (request.UserId ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(userId))
            {
                return BadRequest(new { error = "InvalidUserId", reason = "userId is required for internal deploy calls" });
            }

            if (!string.Equals(app.OwnerUserId, userId, StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            Activity.Current?.SetTag("userId", userId);

            var context = new StructuredLogContext
            {
                CorrelationId = correlationId,
                UserId = userId,
                AppId = appId
            };

            var latest = await _builds.GetByAppIdAsync(appId, cancellationToken);
            if (latest is null || !string.Equals(latest.Status, "built", StringComparison.OrdinalIgnoreCase))
            {
                _logs.Warn("Deploy.BuildNotCompleted", context, new { buildId = latest?.BuildId, status = latest?.Status });
                return Conflict(new { error = "build_not_completed", message = "Build must complete before deploy.", correlationId });
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

            _logs.Info("Deploy.Requested", context, new
            {
                internalCall = true,
                repoName = request.RepoName,
                branch = request.Branch
            });

            try
            {
                var job = await _deployment.QueueDeploymentAsync(appId, userId, app.Name, correlationId, request, cancellationToken);

                _logs.Info("Deploy.Queued", context, new { jobId = job.Id });

                return Accepted(new DeployAppAcceptedResponse(
                    JobId: job.Id ?? string.Empty,
                    Status: "queued",
                    EstimatedCompletionSeconds: 60));
            }
            catch (Exception ex)
            {
                _logs.Error("Deploy.QueueFailed", context, new { error = ex.Message, type = ex.GetType().Name });
                return BadRequest(new { error = "InvalidRequest", message = ex.Message });
            }
        }

        [HttpGet("jobs/{jobId}")]
        [Authorize]
        public async Task<IActionResult> GetDeploymentJobStatus(string appId, string jobId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(jobId))
            {
                return BadRequest(new { error = "InvalidRequest" });
            }

            var correlationId = GetOrCreateCorrelationId();
            Response.Headers["x-correlation-id"] = correlationId;

            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId) || userId == "unknown")
            {
                return Unauthorized(new { error = "Unauthorized", reason = "MissingUserId" });
            }

            var app = await _apps.GetByIdAsync(appId);
            if (app is null)
            {
                return NotFound(new { error = "NotFound", reason = "AppNotFound" });
            }

            if (!IsPlatformAdmin() && !string.Equals(app.OwnerUserId, userId, StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            var job = await _deployment.GetJobAsync(jobId, cancellationToken);
            if (job is null || !string.Equals(job.AppId, appId, StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new { error = "NotFound", reason = "JobNotFound" });
            }

            return Ok(new DeploymentJobStatusResponse(
                JobId: job.Id ?? string.Empty,
                AppId: job.AppId,
                Status: job.Status.ToString().ToLowerInvariant(),
                RepoUrl: job.RepoUrl,
                ErrorMessage: job.ErrorMessage,
                CreatedAt: job.CreatedAt,
                StartedAt: job.StartedAt,
                CompletedAt: job.CompletedAt));
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
