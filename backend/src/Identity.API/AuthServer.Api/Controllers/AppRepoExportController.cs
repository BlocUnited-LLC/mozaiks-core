using System.Diagnostics;
using AuthServer.Api.DTOs;
using AuthServer.Api.Models;
using AuthServer.Api.Services;
using AuthServer.Api.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mozaiks.Auth;

namespace AuthServer.Api.Controllers
{
    [ApiController]
    [Route("api/apps/{appId}/deploy/repo")]
    public sealed class AppRepoExportController : ControllerBase
    {
        private readonly MozaiksAppService _apps;
        private readonly IGitHubRepoExportService _repoExport;
        private readonly StructuredLogEmitter _logs;
        private readonly IUserContextAccessor _userContextAccessor;

        public AppRepoExportController(
            MozaiksAppService apps,
            IGitHubRepoExportService repoExport,
            StructuredLogEmitter logs,
            IUserContextAccessor userContextAccessor)
        {
            _apps = apps;
            _repoExport = repoExport;
            _logs = logs;
            _userContextAccessor = userContextAccessor;
        }

        [HttpPost("manifest")]
        [Authorize]
        public async Task<IActionResult> CreateRepoManifest(string appId, [FromBody] RepoManifestRequest request, CancellationToken cancellationToken)
        {
            var correlationId = GetOrCreateCorrelationId();
            Response.Headers["x-correlation-id"] = correlationId;

            Activity.Current?.SetTag("correlationId", correlationId);
            Activity.Current?.SetTag("appId", appId);

            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId) || userId == "unknown")
            {
                return Unauthorized(new { error = "Unauthorized", reason = "MissingUserId", correlationId });
            }

            Activity.Current?.SetTag("userId", userId);

            var context = new StructuredLogContext { CorrelationId = correlationId, UserId = userId, AppId = appId };
            _logs.Info("Deploy.Repo.Manifest.Requested", context, new { internalCall = false });

            if (string.IsNullOrWhiteSpace(appId))
            {
                return BadRequest(new { error = "InvalidAppId", correlationId });
            }

            var app = await _apps.GetByIdAsync(appId);
            if (app is null || app.IsDeleted || app.Status == AppStatus.Deleted)
            {
                return NotFound(new { error = "NotFound", correlationId });
            }

            if (!IsPlatformAdmin() && !string.Equals(app.OwnerUserId, userId, StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            if (!TryResolveRepoUrl(app, request.RepoUrl, isInternalCall: false, out var repoUrlOrFullName, out var resolveError))
            {
                return StatusCode(resolveError.StatusCode, new { error = resolveError.Error, message = resolveError.Message, correlationId });
            }

            try
            {
                var (_, manifest) = await _repoExport.BuildManifestAsync(repoUrlOrFullName, cancellationToken);
                _logs.Info("Deploy.Repo.Manifest.Completed", context, new { fileCount = manifest.Files.Count });
                return Ok(manifest);
            }
            catch (Exception ex)
            {
                _logs.Warn("Deploy.Repo.Manifest.Failed", context, new { error = ex.Message });
                return StatusCode(500, new
                {
                    error = "repo_manifest_failed",
                    message = "Failed to generate repository manifest.",
                    correlationId
                });
            }
        }

        [HttpPost("/api/internal/apps/{appId}/deploy/repo/manifest")]
        [Authorize(Policy = MozaiksAuthDefaults.InternalServicePolicy)]
        public async Task<IActionResult> CreateRepoManifestInternal(
            string appId,
            [FromBody] RepoManifestRequest request,
            CancellationToken cancellationToken)
        {
            var correlationId = GetOrCreateCorrelationId();
            Response.Headers["x-correlation-id"] = correlationId;

            Activity.Current?.SetTag("correlationId", correlationId);
            Activity.Current?.SetTag("appId", appId);

            var userId = (request.UserId ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(userId))
            {
                return BadRequest(new { error = "InvalidUserId", reason = "userId is required for internal calls", correlationId });
            }

            Activity.Current?.SetTag("userId", userId);

            var context = new StructuredLogContext { CorrelationId = correlationId, UserId = userId, AppId = appId };
            _logs.Info("Deploy.Repo.Manifest.Requested", context, new { internalCall = true });

            if (string.IsNullOrWhiteSpace(appId))
            {
                return BadRequest(new { error = "InvalidAppId", correlationId });
            }

            var app = await _apps.GetByIdAsync(appId);
            if (app is null || app.IsDeleted || app.Status == AppStatus.Deleted)
            {
                return NotFound(new { error = "NotFound", correlationId });
            }

            if (!string.Equals(app.OwnerUserId, userId, StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            if (!TryResolveRepoUrl(app, request.RepoUrl, isInternalCall: true, out var repoUrlOrFullName, out var resolveError))
            {
                return StatusCode(resolveError.StatusCode, new { error = resolveError.Error, message = resolveError.Message, correlationId });
            }

            try
            {
                var (_, manifest) = await _repoExport.BuildManifestAsync(repoUrlOrFullName, cancellationToken);
                _logs.Info("Deploy.Repo.Manifest.Completed", context, new { fileCount = manifest.Files.Count });
                return Ok(manifest);
            }
            catch (Exception ex)
            {
                _logs.Warn("Deploy.Repo.Manifest.Failed", context, new { error = ex.Message });
                return StatusCode(500, new
                {
                    error = "repo_manifest_failed",
                    message = "Failed to generate repository manifest.",
                    correlationId
                });
            }
        }

        [HttpPost("pull-requests")]
        [Authorize]
        public async Task<IActionResult> CreatePullRequest(string appId, [FromBody] CreatePullRequestRequest request, CancellationToken cancellationToken)
        {
            var correlationId = GetOrCreateCorrelationId();
            Response.Headers["x-correlation-id"] = correlationId;

            Activity.Current?.SetTag("correlationId", correlationId);
            Activity.Current?.SetTag("appId", appId);

            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId) || userId == "unknown")
            {
                return Unauthorized(new { error = "Unauthorized", reason = "MissingUserId", correlationId });
            }

            Activity.Current?.SetTag("userId", userId);

            var context = new StructuredLogContext { CorrelationId = correlationId, UserId = userId, AppId = appId };
            _logs.Info("Deploy.Repo.PullRequest.Requested", context, new { internalCall = false });

            if (string.IsNullOrWhiteSpace(appId))
            {
                return BadRequest(new { error = "InvalidAppId", correlationId });
            }

            var app = await _apps.GetByIdAsync(appId);
            if (app is null || app.IsDeleted || app.Status == AppStatus.Deleted)
            {
                return NotFound(new { error = "NotFound", correlationId });
            }

            if (!IsPlatformAdmin() && !string.Equals(app.OwnerUserId, userId, StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            if (!TryResolveRepoUrl(app, request.RepoUrl, isInternalCall: false, out var repoUrlOrFullName, out var resolveError))
            {
                return StatusCode(resolveError.StatusCode, new { error = resolveError.Error, message = resolveError.Message, correlationId });
            }

            if (string.IsNullOrWhiteSpace(request.BaseCommitSha) ||
                string.IsNullOrWhiteSpace(request.BranchName) ||
                string.IsNullOrWhiteSpace(request.Title))
            {
                return BadRequest(new { error = "bad_request", message = "baseCommitSha, branchName, and title are required", correlationId });
            }

            try
            {
                var (_, prUrl) = await _repoExport.CreatePullRequestAsync(repoUrlOrFullName, request, cancellationToken);
                _logs.Info("Deploy.Repo.PullRequest.Completed", context, new { prUrl });
                return Ok(new CreatePullRequestResponse { PrUrl = prUrl });
            }
            catch (Exception ex)
            {
                _logs.Warn("Deploy.Repo.PullRequest.Failed", context, new { error = ex.Message });
                return StatusCode(500, new
                {
                    error = "create_pr_failed",
                    message = "Failed to create pull request.",
                    correlationId
                });
            }
        }

        [HttpPost("/api/internal/apps/{appId}/deploy/repo/pull-requests")]
        [Authorize(Policy = MozaiksAuthDefaults.InternalServicePolicy)]
        public async Task<IActionResult> CreatePullRequestInternal(
            string appId,
            [FromBody] CreatePullRequestRequest request,
            CancellationToken cancellationToken)
        {
            var correlationId = GetOrCreateCorrelationId();
            Response.Headers["x-correlation-id"] = correlationId;

            Activity.Current?.SetTag("correlationId", correlationId);
            Activity.Current?.SetTag("appId", appId);

            var userId = (request.UserId ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(userId))
            {
                return BadRequest(new { error = "InvalidUserId", reason = "userId is required for internal calls", correlationId });
            }

            Activity.Current?.SetTag("userId", userId);

            var context = new StructuredLogContext { CorrelationId = correlationId, UserId = userId, AppId = appId };
            _logs.Info("Deploy.Repo.PullRequest.Requested", context, new { internalCall = true });

            if (string.IsNullOrWhiteSpace(appId))
            {
                return BadRequest(new { error = "InvalidAppId", correlationId });
            }

            var app = await _apps.GetByIdAsync(appId);
            if (app is null || app.IsDeleted || app.Status == AppStatus.Deleted)
            {
                return NotFound(new { error = "NotFound", correlationId });
            }

            if (!string.Equals(app.OwnerUserId, userId, StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            if (!TryResolveRepoUrl(app, request.RepoUrl, isInternalCall: true, out var repoUrlOrFullName, out var resolveError))
            {
                return StatusCode(resolveError.StatusCode, new { error = resolveError.Error, message = resolveError.Message, correlationId });
            }

            if (string.IsNullOrWhiteSpace(request.BaseCommitSha) ||
                string.IsNullOrWhiteSpace(request.BranchName) ||
                string.IsNullOrWhiteSpace(request.Title))
            {
                return BadRequest(new { error = "bad_request", message = "baseCommitSha, branchName, and title are required", correlationId });
            }

            try
            {
                var (_, prUrl) = await _repoExport.CreatePullRequestAsync(repoUrlOrFullName, request, cancellationToken);
                _logs.Info("Deploy.Repo.PullRequest.Completed", context, new { prUrl });
                return Ok(new CreatePullRequestResponse { PrUrl = prUrl });
            }
            catch (Exception ex)
            {
                _logs.Warn("Deploy.Repo.PullRequest.Failed", context, new { error = ex.Message });
                return StatusCode(500, new
                {
                    error = "create_pr_failed",
                    message = "Failed to create pull request.",
                    correlationId
                });
            }
        }

        private sealed record RepoResolveError(int StatusCode, string Error, string Message);

        private bool TryResolveRepoUrl(
            MozaiksAppModel app,
            string? requestRepoUrl,
            bool isInternalCall,
            out string repoUrlOrFullName,
            out RepoResolveError error)
        {
            repoUrlOrFullName = string.Empty;
            error = new RepoResolveError(400, "bad_request", "Invalid repoUrl");

            var requested = (requestRepoUrl ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(requested))
            {
                if (!string.IsNullOrWhiteSpace(app.GitHubRepoFullName))
                {
                    requested = app.GitHubRepoFullName!;
                }
                else if (!string.IsNullOrWhiteSpace(app.GitHubRepoUrl))
                {
                    requested = app.GitHubRepoUrl!;
                }
                else
                {
                    error = new RepoResolveError(
                        isInternalCall ? 400 : 409,
                        isInternalCall ? "bad_request" : "repo_not_configured",
                        isInternalCall ? "repoUrl is required" : "This app does not have a GitHub repo configured.");
                    return false;
                }
            }

            if (!_repoExport.TryParseRepoFullName(requested, out var requestedFullName))
            {
                error = new RepoResolveError(400, "bad_request", "repoUrl must be a GitHub URL or <owner>/<repo>");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(app.GitHubRepoFullName))
            {
                var expected = app.GitHubRepoFullName!.Trim();
                if (!string.Equals(expected, requestedFullName, StringComparison.OrdinalIgnoreCase))
                {
                    error = new RepoResolveError(403, "repo_mismatch", "repoUrl does not match the app's configured repository.");
                    return false;
                }
            }
            else if (!string.IsNullOrWhiteSpace(app.GitHubRepoUrl) && _repoExport.TryParseRepoFullName(app.GitHubRepoUrl, out var expectedFromUrl))
            {
                if (!string.Equals(expectedFromUrl, requestedFullName, StringComparison.OrdinalIgnoreCase))
                {
                    error = new RepoResolveError(403, "repo_mismatch", "repoUrl does not match the app's configured repository.");
                    return false;
                }
            }
            else if (!isInternalCall)
            {
                // User calls must have a verifiable repo binding on the app record.
                error = new RepoResolveError(409, "repo_not_configured", "This app does not have a GitHub repo configured.");
                return false;
            }

            repoUrlOrFullName = requested;
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
