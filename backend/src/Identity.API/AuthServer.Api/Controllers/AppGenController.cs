using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using AuthServer.Api.DTOs;
using AuthServer.Api.Models;
using AuthServer.Api.Services;
using AuthServer.Api.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mozaiks.Auth;

namespace AuthServer.Api.Controllers;

/// <summary>
/// MozaiksAI Tool API: Provides endpoints for app generation workflow.
/// All endpoints require internal service (client-credentials) JWTs.
/// SECURITY: Never return secrets (API keys, tokens, credentials) in responses.
/// </summary>
[ApiController]
[Authorize(Policy = MozaiksAuthDefaults.InternalServicePolicy)]
public sealed class AppGenController : ControllerBase
{
    private readonly MozaiksAppService _apps;
    private readonly IGitHubRepoExportService _repoExport;
    private readonly IGitHubIntegrationService _gitHub;
    private readonly IDeploymentTemplateService _templates;
    private readonly IScaffoldService _scaffold;
    private readonly StructuredLogEmitter _logs;
    private readonly IUserContextAccessor _userContextAccessor;

    public AppGenController(
        MozaiksAppService apps,
        IGitHubRepoExportService repoExport,
        IGitHubIntegrationService gitHub,
        IDeploymentTemplateService templates,
        IScaffoldService scaffold,
        StructuredLogEmitter logs,
        IUserContextAccessor userContextAccessor)
    {
        _apps = apps;
        _repoExport = repoExport;
        _gitHub = gitHub;
        _templates = templates;
        _scaffold = scaffold;
        _logs = logs;
        _userContextAccessor = userContextAccessor;
    }

    /// <summary>
    /// Get app generation spec (non-sensitive data only).
    /// Returns TechStack, design inputs, and metadata needed for code generation.
    /// NEVER returns: API keys, GitHub tokens, Azure credentials, DockerHub tokens, connection strings.
    /// </summary>
    [HttpGet("api/apps/{appId}/appgen/spec")]
    public async Task<IActionResult> GetAppSpec(string appId)
    {
        var correlationId = GetOrCreateCorrelationId();
        Response.Headers["x-correlation-id"] = correlationId;

        Activity.Current?.SetTag("correlationId", correlationId);
        Activity.Current?.SetTag("appId", appId);

        var (isAuthorized, userId, isInternalCall) = ValidateAuth();
        if (!isAuthorized)
        {
            return Unauthorized(new { error = "Unauthorized", correlationId });
        }

        Activity.Current?.SetTag("userId", userId);

        var context = new StructuredLogContext { CorrelationId = correlationId, UserId = userId, AppId = appId };
        _logs.Info("AppGen.Spec.Requested", context, new { internalCall = isInternalCall });

        if (string.IsNullOrWhiteSpace(appId))
        {
            return BadRequest(new { error = "InvalidAppId", correlationId });
        }

        var app = await _apps.GetByIdAsync(appId);
        if (app is null || app.IsDeleted || app.Status == AppStatus.Deleted)
        {
            return NotFound(new { error = "NotFound", correlationId });
        }

        if (!isInternalCall && !IsPlatformAdmin() && !string.Equals(app.OwnerUserId, userId, StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        // Build spec response - NO SECRETS
        var spec = new AppGenSpecResponse
        {
            AppId = app.Id ?? appId,
            AppName = app.Name,
            Description = app.Description,
            Industry = app.Industry,
            OwnerUserId = app.OwnerUserId,
            FeatureFlags = app.FeatureFlags,
            GitHubRepoFullName = app.GitHubRepoFullName,
            DatabaseName = app.DatabaseName,
            Status = app.Status.ToString(),
            // TechStack would be loaded from a separate collection or embedded in app
            // For now, return null - to be populated when TechStack is stored
            TechStack = null
        };

        _logs.Info("AppGen.Spec.Completed", context, new { hasRepo = !string.IsNullOrWhiteSpace(app.GitHubRepoFullName) });
        return Ok(spec);
    }

    /// <summary>
    /// Export initial code files to a new or existing repository.
    /// Creates repo if requested, pushes files, returns repo URL and base commit SHA.
    /// </summary>
    [HttpPost("api/apps/{appId}/deploy/repo/initial-export")]
    public async Task<IActionResult> InitialExport(string appId, [FromBody] InitialExportRequest request, CancellationToken cancellationToken)
    {
        var correlationId = GetOrCreateCorrelationId();
        Response.Headers["x-correlation-id"] = correlationId;

        Activity.Current?.SetTag("correlationId", correlationId);
        Activity.Current?.SetTag("appId", appId);

        var (isAuthorized, userId, isInternalCall) = ValidateAuth(request.UserId);
        if (!isAuthorized)
        {
            return Unauthorized(new { error = "Unauthorized", correlationId });
        }

        Activity.Current?.SetTag("userId", userId);

        var context = new StructuredLogContext { CorrelationId = correlationId, UserId = userId, AppId = appId };
        _logs.Info("AppGen.InitialExport.Requested", context, new { internalCall = isInternalCall, createRepo = request.CreateRepo });

        if (string.IsNullOrWhiteSpace(appId))
        {
            return BadRequest(new { error = "InvalidAppId", correlationId });
        }

        var app = await _apps.GetByIdAsync(appId);
        if (app is null || app.IsDeleted || app.Status == AppStatus.Deleted)
        {
            return NotFound(new { error = "NotFound", correlationId });
        }

        if (!isInternalCall && !IsPlatformAdmin() && !string.Equals(app.OwnerUserId, userId, StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        if (request.Files is null || request.Files.Count == 0)
        {
            return BadRequest(new { error = "bad_request", message = "files array is required", correlationId });
        }

        try
        {
            string repoFullName;
            string repoUrl;

            // Create new repo or use existing
            if (request.CreateRepo)
            {
                var repoName = string.IsNullOrWhiteSpace(request.RepoName)
                    ? $"mozaiks-{app.Name?.ToLowerInvariant().Replace(" ", "-") ?? appId}"
                    : request.RepoName;

                var createResult = await _gitHub.CreateRepositoryAsync(new CreateRepoRequest
                {
                    Name = repoName,
                    Description = $"Mozaiks app: {app.Name}",
                    IsPrivate = true,
                    OwnerId = userId,
                    AppId = appId
                }, cancellationToken);

                if (!createResult.Success)
                {
                    return StatusCode(500, new
                    {
                        error = "repo_create_failed",
                        message = createResult.ErrorMessage ?? "Failed to create repository",
                        correlationId
                    });
                }

                repoFullName = createResult.RepoFullName;
                repoUrl = createResult.RepoUrl;

                // Update app with repo info
                await _apps.UpdateGitHubRepoAsync(appId, repoFullName, repoUrl);
            }
            else
            {
                // Use existing repo
                var requestedUrl = (request.RepoUrl ?? app.GitHubRepoFullName ?? app.GitHubRepoUrl ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(requestedUrl))
                {
                    return BadRequest(new { error = "bad_request", message = "repoUrl is required when createRepo=false", correlationId });
                }

                if (!_repoExport.TryParseRepoFullName(requestedUrl, out repoFullName))
                {
                    return BadRequest(new { error = "bad_request", message = "Invalid repoUrl format", correlationId });
                }

                repoUrl = $"https://github.com/{repoFullName}";
            }

            // Get current repo state
            var (_, manifest) = await _repoExport.BuildManifestAsync(repoFullName, cancellationToken);
            var baseCommitSha = manifest.BaseCommitSha;

            // Convert files to GitHubFile format and push
            var gitHubFiles = request.Files
                .Where(f => !string.IsNullOrWhiteSpace(f.Path) && (f.Operation ?? "add").ToLowerInvariant() != "delete")
                .Select(f => new GitHubFile
                {
                    Path = f.Path!,
                    ContentBase64 = f.ContentBase64,
                    Content = string.IsNullOrWhiteSpace(f.ContentBase64) ? Encoding.UTF8.GetBytes(f.Content ?? string.Empty) : null
                })
                .ToList();

            var commitMessage = string.IsNullOrWhiteSpace(request.CommitMessage)
                ? "Initial export from MozaiksAI"
                : request.CommitMessage;

            var pushSuccess = await _gitHub.PushFilesAsync(repoFullName, gitHubFiles, commitMessage, "main", cancellationToken);
            if (!pushSuccess)
            {
                return StatusCode(500, new
                {
                    error = "push_failed",
                    message = "Failed to push files to repository",
                    correlationId
                });
            }

            // Get new commit SHA after push
            var (_, newManifest) = await _repoExport.BuildManifestAsync(repoFullName, cancellationToken);

            _logs.Info("AppGen.InitialExport.Completed", context, new { repoFullName, fileCount = request.Files.Count });

            return Ok(new InitialExportResponse
            {
                Success = true,
                RepoUrl = repoUrl,
                RepoFullName = repoFullName,
                BaseCommitSha = newManifest.BaseCommitSha
            });
        }
        catch (Exception ex)
        {
            _logs.Warn("AppGen.InitialExport.Failed", context, new { error = ex.Message });
            return StatusCode(500, new
            {
                error = "initial_export_failed",
                message = "Failed to export files to repository",
                correlationId
            });
        }
    }

    /// <summary>
    /// Generate a complete app scaffold including all framework-specific boilerplate.
    /// This endpoint replaces FileManager's framework-specific file generation logic.
    /// Returns: index.html, env.js, nginx.conf, entrypoint.sh, __init__.py files,
    /// package.json/requirements.txt, Dockerfiles, and GitHub Actions workflow.
    /// </summary>
    [HttpPost("api/apps/{appId}/deploy/scaffold")]
    public async Task<IActionResult> GenerateScaffold(string appId, [FromBody] GenerateScaffoldRequest request, CancellationToken cancellationToken)
    {
        var correlationId = GetOrCreateCorrelationId();
        Response.Headers["x-correlation-id"] = correlationId;

        Activity.Current?.SetTag("correlationId", correlationId);
        Activity.Current?.SetTag("appId", appId);

        var (isAuthorized, userId, isInternalCall) = ValidateAuth(request.UserId);
        if (!isAuthorized)
        {
            return Unauthorized(new { error = "Unauthorized", correlationId });
        }

        Activity.Current?.SetTag("userId", userId);

        var context = new StructuredLogContext { CorrelationId = correlationId, UserId = userId, AppId = appId };
        _logs.Info("AppGen.Scaffold.Requested", context, new 
        { 
            internalCall = isInternalCall,
            includeBoilerplate = request.IncludeBoilerplate,
            includeDockerfiles = request.IncludeDockerfiles,
            includeWorkflow = request.IncludeWorkflow,
            includeInitFiles = request.IncludeInitFiles
        });

        if (string.IsNullOrWhiteSpace(appId))
        {
            return BadRequest(new { error = "InvalidAppId", correlationId });
        }

        try
        {
            var result = await _scaffold.GenerateScaffoldAsync(appId, request);

            if (!result.Success)
            {
                _logs.Warn("AppGen.Scaffold.Failed", context, new { error = result.Error });
                return StatusCode(500, new
                {
                    error = "scaffold_generation_failed",
                    message = result.Error ?? "Failed to generate scaffold",
                    correlationId
                });
            }

            _logs.Info("AppGen.Scaffold.Completed", context, new 
            { 
                fileCount = result.Files.Count,
                categories = result.Summary?.Categories
            });

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logs.Warn("AppGen.Scaffold.Failed", context, new { error = ex.Message });
            return StatusCode(500, new
            {
                error = "scaffold_generation_failed",
                message = "Failed to generate app scaffold",
                correlationId
            });
        }
    }

    /// <summary>
    /// Get supported tech stacks for app creation.
    /// Returns list of supported frontend/backend frameworks and default stack.
    /// </summary>
    [HttpGet("api/appgen/supported-stacks")]
    public async Task<IActionResult> GetSupportedTechStacks()
    {
        var correlationId = GetOrCreateCorrelationId();
        Response.Headers["x-correlation-id"] = correlationId;

        var (isAuthorized, _, _) = ValidateAuth();
        if (!isAuthorized)
        {
            return Unauthorized(new { error = "Unauthorized", correlationId });
        }

        var stacks = await _scaffold.GetSupportedTechStacksAsync();
        return Ok(stacks);
    }

    /// <summary>
    /// Generate deployment templates (Dockerfiles, GitHub Actions workflows).
    /// Uses DeploymentManager + Jinja-style templates.
    /// </summary>
    [HttpPost("api/apps/{appId}/deploy/templates/generate")]
    public async Task<IActionResult> GenerateTemplates(string appId, [FromBody] GenerateTemplatesRequest request, CancellationToken cancellationToken)
    {
        var correlationId = GetOrCreateCorrelationId();
        Response.Headers["x-correlation-id"] = correlationId;

        Activity.Current?.SetTag("correlationId", correlationId);
        Activity.Current?.SetTag("appId", appId);

        var (isAuthorized, userId, isInternalCall) = ValidateAuth(request.UserId);
        if (!isAuthorized)
        {
            return Unauthorized(new { error = "Unauthorized", correlationId });
        }

        Activity.Current?.SetTag("userId", userId);

        var context = new StructuredLogContext { CorrelationId = correlationId, UserId = userId, AppId = appId };
        _logs.Info("AppGen.Templates.Requested", context, new { internalCall = isInternalCall });

        if (string.IsNullOrWhiteSpace(appId))
        {
            return BadRequest(new { error = "InvalidAppId", correlationId });
        }

        var app = await _apps.GetByIdAsync(appId);
        if (app is null || app.IsDeleted || app.Status == AppStatus.Deleted)
        {
            return NotFound(new { error = "NotFound", correlationId });
        }

        if (!isInternalCall && !IsPlatformAdmin() && !string.Equals(app.OwnerUserId, userId, StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        try
        {
            // Map DTO to service input
            var input = new GenerateTemplatesInput
            {
                AppId = appId,
                AppName = app.Name,
                IncludeWorkflow = request.IncludeWorkflow,
                IncludeDockerfiles = request.IncludeDockerfiles,
                TechStack = request.TechStack is not null ? new TechStackInput
                {
                    Frontend = request.TechStack.Frontend is not null ? new FrameworkInput
                    {
                        Framework = request.TechStack.Frontend.Framework,
                        Language = request.TechStack.Frontend.Language,
                        Version = request.TechStack.Frontend.Version,
                        Port = request.TechStack.Frontend.Port ?? 3000,
                        EntryPoint = request.TechStack.Frontend.EntryPoint
                    } : null,
                    Backend = request.TechStack.Backend is not null ? new FrameworkInput
                    {
                        Framework = request.TechStack.Backend.Framework,
                        Language = request.TechStack.Backend.Language,
                        Version = request.TechStack.Backend.Version,
                        Port = request.TechStack.Backend.Port ?? 8000,
                        EntryPoint = request.TechStack.Backend.EntryPoint
                    } : null,
                    Database = request.TechStack.Database is not null ? new DatabaseInput
                    {
                        Type = request.TechStack.Database.Type,
                        Provider = request.TechStack.Database.Provider
                    } : null
                } : null
            };

            var result = _templates.GenerateTemplates(input);

            if (!result.Success)
            {
                return StatusCode(500, new
                {
                    error = "template_generation_failed",
                    message = result.Error ?? "Failed to generate templates",
                    correlationId
                });
            }

            var response = new GenerateTemplatesResponse
            {
                Success = true,
                Files = result.Files.Select(f => new GeneratedTemplateFile
                {
                    Path = f.Path,
                    ContentBase64 = Convert.ToBase64String(f.Content),
                    Description = f.Description
                }).ToList()
            };

            _logs.Info("AppGen.Templates.Completed", context, new { fileCount = response.Files.Count });
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logs.Warn("AppGen.Templates.Failed", context, new { error = ex.Message });
            return StatusCode(500, new
            {
                error = "template_generation_failed",
                message = "Failed to generate deployment templates",
                correlationId
            });
        }
    }

    /// <summary>
    /// Set GitHub repository secrets for CI/CD.
    /// Configures DockerHub, Azure credentials, and other deployment secrets.
    /// Internal use only (MozaiksAI runtime).
    /// </summary>
    [HttpPost("api/apps/{appId}/deploy/repo/secrets")]
    public async Task<IActionResult> SetRepositorySecrets(string appId, [FromBody] SetRepositorySecretsRequest request, CancellationToken cancellationToken)
    {
        var correlationId = GetOrCreateCorrelationId();
        Response.Headers["x-correlation-id"] = correlationId;

        Activity.Current?.SetTag("correlationId", correlationId);
        Activity.Current?.SetTag("appId", appId);

        var (isAuthorized, userId, isInternalCall) = ValidateAuth(request.UserId);
        if (!isAuthorized)
        {
            return Unauthorized(new { error = "Unauthorized", correlationId });
        }

        // Secrets endpoint is internal-only for security
        if (!isInternalCall && !IsPlatformAdmin())
        {
            return Forbid();
        }

        Activity.Current?.SetTag("userId", userId);

        var context = new StructuredLogContext { CorrelationId = correlationId, UserId = userId, AppId = appId };
        _logs.Info("AppGen.Secrets.Requested", context, new { internalCall = isInternalCall });

        if (string.IsNullOrWhiteSpace(appId))
        {
            return BadRequest(new { error = "InvalidAppId", correlationId });
        }

        var app = await _apps.GetByIdAsync(appId);
        if (app is null || app.IsDeleted || app.Status == AppStatus.Deleted)
        {
            return NotFound(new { error = "NotFound", correlationId });
        }

        // Get repo from request or app
        var repoFullName = !string.IsNullOrWhiteSpace(request.RepoFullName)
            ? request.RepoFullName.Trim()
            : app.GitHubRepoFullName ?? string.Empty;

        if (string.IsNullOrWhiteSpace(repoFullName))
        {
            return BadRequest(new { error = "bad_request", message = "Repository not found. Provide repoFullName or ensure app has a linked repo.", correlationId });
        }

        try
        {
            var result = await _repoExport.SetRepositorySecretsAsync(appId, repoFullName, request, cancellationToken);

            if (!result.Success)
            {
                return StatusCode(500, new
                {
                    error = "secrets_failed",
                    message = result.Error ?? "Failed to set repository secrets",
                    correlationId
                });
            }

            _logs.Info("AppGen.Secrets.Completed", context, new { secretsSet = result.SecretsSet?.Count ?? 0, includedDb = request.IncludeDatabaseUri });
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logs.Warn("AppGen.Secrets.Failed", context, new { error = ex.Message });
            return StatusCode(500, new
            {
                error = "secrets_failed",
                message = "Failed to set repository secrets",
                correlationId
            });
        }
    }

    /// <summary>
    /// Get deployment status for an app.
    /// Returns workflow run status, deployment URLs (preview, production), and artifact URLs.
    /// </summary>
    [HttpGet("api/apps/{appId}/deploy/status")]
    public async Task<IActionResult> GetDeploymentStatus(string appId, [FromQuery] string? repoFullName, CancellationToken cancellationToken)
    {
        var correlationId = GetOrCreateCorrelationId();
        Response.Headers["x-correlation-id"] = correlationId;

        Activity.Current?.SetTag("correlationId", correlationId);
        Activity.Current?.SetTag("appId", appId);

        var (isAuthorized, userId, isInternalCall) = ValidateAuth();
        if (!isAuthorized)
        {
            return Unauthorized(new { error = "Unauthorized", correlationId });
        }

        Activity.Current?.SetTag("userId", userId);

        var context = new StructuredLogContext { CorrelationId = correlationId, UserId = userId, AppId = appId };
        _logs.Info("AppGen.DeploymentStatus.Requested", context, new { internalCall = isInternalCall });

        if (string.IsNullOrWhiteSpace(appId))
        {
            return BadRequest(new { error = "InvalidAppId", correlationId });
        }

        var app = await _apps.GetByIdAsync(appId);
        if (app is null || app.IsDeleted || app.Status == AppStatus.Deleted)
        {
            return NotFound(new { error = "NotFound", correlationId });
        }

        if (!isInternalCall && !IsPlatformAdmin() && !string.Equals(app.OwnerUserId, userId, StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        // Get repo from query param or app
        var repo = !string.IsNullOrWhiteSpace(repoFullName)
            ? repoFullName.Trim()
            : app.GitHubRepoFullName ?? string.Empty;

        if (string.IsNullOrWhiteSpace(repo))
        {
            return BadRequest(new { error = "bad_request", message = "Repository not found. Provide repoFullName or ensure app has a linked repo.", correlationId });
        }

        try
        {
            var request = new GetDeploymentStatusRequest
            {
                AppId = appId,
                RepoFullName = repo,
                UserId = userId
            };

            var result = await _repoExport.GetDeploymentStatusAsync(repo, request, cancellationToken);

            if (!result.Success)
            {
                return StatusCode(500, new
                {
                    error = "deployment_status_failed",
                    message = result.Error ?? "Failed to get deployment status",
                    correlationId
                });
            }

            _logs.Info("AppGen.DeploymentStatus.Completed", context, new { status = result.Status });
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logs.Warn("AppGen.DeploymentStatus.Failed", context, new { error = ex.Message });
            return StatusCode(500, new
            {
                error = "deployment_status_failed",
                message = "Failed to get deployment status",
                correlationId
            });
        }
    }

    private (bool isAuthorized, string userId, bool isInternalCall) ValidateAuth(string? requestUserId = null)
    {
        var userId = (requestUserId ?? "internal").Trim();
        if (string.IsNullOrWhiteSpace(userId))
        {
            userId = "internal";
        }

        return (true, userId, true);
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
