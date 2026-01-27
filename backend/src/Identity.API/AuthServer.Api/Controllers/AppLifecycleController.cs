using AuthServer.Api.Models;
using AuthServer.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthServer.Api.Controllers;

/// <summary>
/// Exposes app lifecycle state and phase resolution.
/// </summary>
[ApiController]
[Route("api/apps")]
public sealed class AppLifecycleController : ControllerBase
{
    private readonly IAppLifecyclePhaseResolver _resolver;
    private readonly ILogger<AppLifecycleController> _logger;

    public AppLifecycleController(
        IAppLifecyclePhaseResolver resolver,
        ILogger<AppLifecycleController> logger)
    {
        _resolver = resolver;
        _logger = logger;
    }

    /// <summary>
    /// Get the canonical lifecycle state for an app.
    /// Returns the computed phase, timestamps, allowed/blocked actions, and underlying details.
    /// </summary>
    /// <param name="appId">The app ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The full lifecycle state.</returns>
    [HttpGet("{appId}/lifecycle")]
    [Authorize]
    [ProducesResponseType(typeof(AppLifecycleStateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AppLifecycleStateResponse>> GetLifecycle(
        string appId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return BadRequest(new { error = "appId is required" });
        }

        var state = await _resolver.ResolveAsync(appId, cancellationToken);
        if (state == null)
        {
            return NotFound(new { error = "App not found" });
        }

        return Ok(new AppLifecycleStateResponse
        {
            AppId = state.AppId,
            Phase = state.Phase.ToString(),
            PhaseOrdinal = (int)state.Phase,
            PhaseSince = state.PhaseSince,
            AllowedActions = state.AllowedActions,
            BlockedActions = state.BlockedActions,
            ErrorMessage = state.ErrorMessage,
            Details = new AppLifecycleDetailsResponse
            {
                AppStatus = state.Details.AppStatus,
                BuildStatus = state.Details.BuildStatus,
                PreviewUrl = state.Details.PreviewUrl,
                HostedStatus = state.Details.HostedStatus,
                HostedUrl = state.Details.HostedUrl,
                ProvisioningJobs = state.Details.ProvisioningJobs
                    .Select(j => new ProvisioningJobSummaryResponse
                    {
                        JobId = j.JobId,
                        JobType = j.JobType,
                        Status = j.Status,
                        StartedAt = j.StartedAt,
                        Error = j.Error
                    })
                    .ToList()
            }
        });
    }

    /// <summary>
    /// Get just the phase (lightweight version of GetLifecycle).
    /// </summary>
    [HttpGet("{appId}/lifecycle/phase")]
    [Authorize]
    [ProducesResponseType(typeof(AppLifecyclePhaseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AppLifecyclePhaseResponse>> GetPhase(
        string appId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return BadRequest(new { error = "appId is required" });
        }

        var phase = await _resolver.ResolvePhaseAsync(appId, cancellationToken);
        if (phase == null)
        {
            return NotFound(new { error = "App not found" });
        }

        return Ok(new AppLifecyclePhaseResponse
        {
            AppId = appId,
            Phase = phase.Value.ToString(),
            PhaseOrdinal = (int)phase.Value
        });
    }
}

#region Response DTOs

public sealed class AppLifecycleStateResponse
{
    public required string AppId { get; init; }
    public required string Phase { get; init; }
    public int PhaseOrdinal { get; init; }
    public DateTime? PhaseSince { get; init; }
    public List<string> AllowedActions { get; init; } = new();
    public List<string> BlockedActions { get; init; } = new();
    public string? ErrorMessage { get; init; }
    public AppLifecycleDetailsResponse Details { get; init; } = new();
}

public sealed class AppLifecycleDetailsResponse
{
    public string? AppStatus { get; init; }
    public string? BuildStatus { get; init; }
    public string? PreviewUrl { get; init; }
    public string? HostedStatus { get; init; }
    public string? HostedUrl { get; init; }
    public List<ProvisioningJobSummaryResponse> ProvisioningJobs { get; init; } = new();
}

public sealed class ProvisioningJobSummaryResponse
{
    public string? JobId { get; init; }
    public string? JobType { get; init; }
    public string? Status { get; init; }
    public DateTime? StartedAt { get; init; }
    public string? Error { get; init; }
}

public sealed class AppLifecyclePhaseResponse
{
    public required string AppId { get; init; }
    public required string Phase { get; init; }
    public int PhaseOrdinal { get; init; }
}

#endregion
