using AuthServer.Api.Models;
using AuthServer.Api.Repository.Interfaces;

namespace AuthServer.Api.Services;

/// <summary>
/// Resolves the canonical AppLifecyclePhase from distributed state.
/// 
/// The phase is computed, not stored. This resolver aggregates state from:
/// - MozaiksApps.Status (AuthServer.Api)
/// - AppBuildStatus.Status (AuthServer.Api)
/// - HostedApp.Status (Hosting.API)
/// - ProvisioningJob.Status (Hosting.API)
/// 
/// Priority order (highest to lowest):
/// 1. Deleted/Archived → Archived
/// 2. Paused/Stopped → Paused
/// 3. HostedApp.Active + App.Running → Live
/// 4. HostedApp.Pending/Provisioning OR jobs running → Provisioning
/// 5. Build.built + PreviewUrl → Preview
/// 6. Build.building → Building
/// 7. Otherwise → Draft
/// 
/// See: CONTROL_PLANE_APPLIFECYCLE.md
/// </summary>
public interface IAppLifecyclePhaseResolver
{
    /// <summary>
    /// Resolves the full lifecycle state for an app.
    /// </summary>
    Task<AppLifecycleState?> ResolveAsync(string appId, CancellationToken cancellationToken);

    /// <summary>
    /// Resolves just the phase (faster, no allowed/blocked actions).
    /// </summary>
    Task<AppLifecyclePhase?> ResolvePhaseAsync(string appId, CancellationToken cancellationToken);
}

public sealed class AppLifecyclePhaseResolver : IAppLifecyclePhaseResolver
{
    private readonly IMozaiksAppRepository _appRepo;
    private readonly IAppBuildStatusRepository _buildRepo;
    private readonly IHostingApiClient _hostingClient;
    private readonly ILogger<AppLifecyclePhaseResolver> _logger;

    // Action definitions by phase
    private static readonly Dictionary<AppLifecyclePhase, (string[] Allowed, string[] Blocked)> PhaseActions = new()
    {
        [AppLifecyclePhase.Draft] = (
            new[] { "edit", "configure", "initiate_build" },
            new[] { "preview", "deploy", "host", "monetize", "publish" }
        ),
        [AppLifecyclePhase.Building] = (
            new[] { "cancel_build", "view_logs" },
            new[] { "preview", "deploy", "host", "edit_config" }
        ),
        [AppLifecyclePhase.Preview] = (
            new[] { "view_preview", "rebuild", "deploy", "edit_config" },
            new[] { "host", "monetize", "publish" }
        ),
        [AppLifecyclePhase.Provisioning] = (
            new[] { "view_status", "cancel" },
            new[] { "edit_config", "deploy", "monetize" }
        ),
        [AppLifecyclePhase.Live] = (
            new[] { "pause", "rebuild", "configure_domain", "monetize", "publish", "scale" },
            Array.Empty<string>()
        ),
        [AppLifecyclePhase.Paused] = (
            new[] { "resume", "archive", "export_data" },
            new[] { "access_runtime", "monetize", "deploy", "publish" }
        ),
        [AppLifecyclePhase.Archived] = (
            new[] { "restore", "export_data" },
            new[] { "runtime", "hosting", "monetize", "publish" }
        )
    };

    public AppLifecyclePhaseResolver(
        IMozaiksAppRepository appRepo,
        IAppBuildStatusRepository buildRepo,
        IHostingApiClient hostingClient,
        ILogger<AppLifecyclePhaseResolver> logger)
    {
        _appRepo = appRepo;
        _buildRepo = buildRepo;
        _hostingClient = hostingClient;
        _logger = logger;
    }

    public async Task<AppLifecycleState?> ResolveAsync(string appId, CancellationToken cancellationToken)
    {
        var app = await _appRepo.GetByIdAsync(appId);
        if (app == null)
        {
            return null;
        }

        var buildStatus = await _buildRepo.GetByAppIdAsync(appId, cancellationToken);
        var hostingStatus = await _hostingClient.GetHostedAppStatusAsync(appId, cancellationToken);

        var (phase, phaseSince, errorMessage) = ComputePhase(app, buildStatus, hostingStatus);

        var (allowed, blocked) = PhaseActions.TryGetValue(phase, out var actions)
            ? actions
            : (Array.Empty<string>(), Array.Empty<string>());

        return new AppLifecycleState
        {
            AppId = appId,
            Phase = phase,
            PhaseSince = phaseSince,
            AllowedActions = allowed.ToList(),
            BlockedActions = blocked.ToList(),
            ErrorMessage = errorMessage,
            Details = new AppLifecycleDetails
            {
                AppStatus = app.Status.ToString(),
                BuildStatus = buildStatus?.Status,
                PreviewUrl = buildStatus?.Artifacts?.PreviewUrl,
                HostedStatus = hostingStatus?.HostedApp?.Status,
                HostedUrl = hostingStatus?.HostedApp?.HostingUrl,
                ProvisioningJobs = hostingStatus?.ProvisioningJobs?
                    .Select(j => new ProvisioningJobSummary
                    {
                        JobId = j.Id,
                        JobType = j.Type,
                        Status = j.Status,
                        StartedAt = j.StartedAt,
                        Error = j.LastError
                    })
                    .ToList() ?? new List<ProvisioningJobSummary>()
            }
        };
    }

    public async Task<AppLifecyclePhase?> ResolvePhaseAsync(string appId, CancellationToken cancellationToken)
    {
        var app = await _appRepo.GetByIdAsync(appId);
        if (app == null)
        {
            return null;
        }

        var buildStatus = await _buildRepo.GetByAppIdAsync(appId, cancellationToken);
        var hostingStatus = await _hostingClient.GetHostedAppStatusAsync(appId, cancellationToken);

        var (phase, _, _) = ComputePhase(app, buildStatus, hostingStatus);
        return phase;
    }

    private (AppLifecyclePhase Phase, DateTime? PhaseSince, string? ErrorMessage) ComputePhase(
        MozaiksAppModel app,
        AppBuildStatusModel? buildStatus,
        HostedAppWithJobsResponse? hostingStatus)
    {
        // Priority 1: Archived/Deleted (terminal state)
        if (app.IsDeleted || app.Status == AppStatus.Deleted || app.Status == AppStatus.Archived)
        {
            return (AppLifecyclePhase.Archived, app.DeletedAt, null);
        }

        // Priority 2: Paused/Stopped
        if (app.Status == AppStatus.Paused || app.Status == AppStatus.Stopped)
        {
            return (AppLifecyclePhase.Paused, app.PausedAt, null);
        }

        var hostedAppStatus = hostingStatus?.HostedApp?.Status?.ToLowerInvariant();
        var hasActiveJobs = hostingStatus?.ProvisioningJobs?.Any(j =>
            j.Status?.ToLowerInvariant() is "queued" or "running") ?? false;

        // Priority 3: Live (Active hosted app + Running app status)
        if (hostedAppStatus == "active" && app.Status == AppStatus.Running)
        {
            return (AppLifecyclePhase.Live, app.DeployedAt, null);
        }

        // Priority 4: Provisioning (pending/provisioning hosted app OR active jobs)
        if (hostedAppStatus is "pending" or "provisioning" || hasActiveJobs)
        {
            var errorJob = hostingStatus?.ProvisioningJobs?.FirstOrDefault(j =>
                j.Status?.ToLowerInvariant() == "failed");
            
            return (AppLifecyclePhase.Provisioning, null, errorJob?.LastError);
        }

        // Priority 4b: Failed provisioning (hosted app failed but not paused)
        if (hostedAppStatus == "failed" && app.Status == AppStatus.Failed)
        {
            var errorMessage = hostingStatus?.ProvisioningJobs?
                .Where(j => !string.IsNullOrEmpty(j.LastError))
                .Select(j => j.LastError)
                .FirstOrDefault();
            
            return (AppLifecyclePhase.Provisioning, null, errorMessage ?? "Provisioning failed");
        }

        // Priority 5: Preview (build succeeded with preview URL)
        var buildStatusLower = buildStatus?.Status?.ToLowerInvariant();
        if (buildStatusLower == "built" && !string.IsNullOrEmpty(buildStatus?.Artifacts?.PreviewUrl))
        {
            return (AppLifecyclePhase.Preview, buildStatus?.CompletedAtUtc, null);
        }

        // Priority 6: Building
        if (buildStatusLower == "building")
        {
            return (AppLifecyclePhase.Building, buildStatus?.StartedAtUtc, null);
        }

        // Priority 6b: Build failed (still in building phase but with error)
        if (buildStatusLower is "error" or "failed")
        {
            return (AppLifecyclePhase.Building, buildStatus?.StartedAtUtc, buildStatus?.Error?.Message);
        }

        // Priority 7: Default to Draft
        return (AppLifecyclePhase.Draft, app.CreatedAt, null);
    }
}
