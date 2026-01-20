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
[Route("api/apps/{appId}/deployments")]
[Authorize]
public sealed class AppDeploymentsController : ControllerBase
{
    private readonly MozaiksAppService _apps;
    private readonly IDeploymentJobRepository _deployments;
    private readonly StructuredLogEmitter _logs;
    private readonly IUserContextAccessor _userContextAccessor;

    public AppDeploymentsController(
        MozaiksAppService apps,
        IDeploymentJobRepository deployments,
        StructuredLogEmitter logs,
        IUserContextAccessor userContextAccessor)
    {
        _apps = apps;
        _deployments = deployments;
        _logs = logs;
        _userContextAccessor = userContextAccessor;
    }

    [HttpGet]
    public async Task<ActionResult<AppDeploymentHistoryResponse>> GetDeploymentHistory(
        string appId,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
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

        var resolvedPage = page.GetValueOrDefault(1);
        var resolvedPageSize = pageSize.GetValueOrDefault(20);

        if (resolvedPage <= 0 || resolvedPageSize <= 0 || resolvedPageSize > 100)
        {
            return BadRequest(new { error = "InvalidPagination", page = resolvedPage, pageSize = resolvedPageSize, maxPageSize = 100 });
        }

        _logs.Info("Apps.Deployments.List.Requested", context, new { page = resolvedPage, pageSize = resolvedPageSize });

        var app = await _apps.GetByIdAsync(appId);
        if (app is null)
        {
            _logs.Warn("Apps.Deployments.List.AppNotFound", context);
            return NotFound(new { error = "NotFound" });
        }

        if (app.IsDeleted || app.Status == AppStatus.Deleted)
        {
            _logs.Warn("Apps.Deployments.List.AppDeleted", context);
            return NotFound(new { error = "NotFound" });
        }

        if (!IsPlatformAdmin() && !string.Equals(app.OwnerUserId, userId, StringComparison.OrdinalIgnoreCase))
        {
            _logs.Warn("Apps.Deployments.List.Forbidden", context);
            return Forbid();
        }

        var (jobs, total) = await _deployments.GetByAppIdAsync(appId, resolvedPage, resolvedPageSize, cancellationToken);

        var items = jobs.Select(job =>
        {
            var startedAt = job.StartedAt ?? job.CreatedAt;
            var completedAt = job.CompletedAt;
            var durationSeconds = 0;
            if (completedAt.HasValue)
            {
                durationSeconds = (int)Math.Max(0, (completedAt.Value - startedAt).TotalSeconds);
            }

            return new AppDeploymentHistoryItem(
                DeploymentId: job.Id ?? string.Empty,
                Status: job.Status.ToString().ToLowerInvariant(),
                StartedAt: startedAt,
                CompletedAt: completedAt,
                DurationSeconds: durationSeconds,
                Trigger: "manual",
                TriggeredBy: job.UserId,
                CommitHash: null,
                CommitMessage: job.CommitMessage,
                RepoUrl: job.RepoUrl,
                Error: job.ErrorMessage,
                Logs: Array.Empty<DeploymentLogEntry>());
        }).ToList();

        _logs.Info("Apps.Deployments.List.Completed", context, new { count = items.Count, total });

        return Ok(new AppDeploymentHistoryResponse(
            AppId: appId,
            TotalDeployments: total,
            Deployments: items));
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
