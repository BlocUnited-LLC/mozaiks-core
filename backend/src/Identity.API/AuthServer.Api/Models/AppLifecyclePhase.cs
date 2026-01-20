namespace AuthServer.Api.Models;

/// <summary>
/// Canonical lifecycle phases for Mozaiks apps.
/// This enum represents the computed phase, not stored state.
/// See CONTROL_PLANE_APPLIFECYCLE.md for full documentation.
/// </summary>
public enum AppLifecyclePhase
{
    /// <summary>
    /// App record exists but has never been built.
    /// The founder is still designing or configuring the app.
    /// </summary>
    Draft = 0,

    /// <summary>
    /// MozaiksAI runtime is generating or regenerating the app code.
    /// A build job is in progress.
    /// </summary>
    Building = 1,

    /// <summary>
    /// A successful build exists. The founder can preview the app
    /// in a sandbox environment. Not yet deployed or hosted.
    /// </summary>
    Preview = 2,

    /// <summary>
    /// The app is being deployed to production infrastructure.
    /// GitHub repo creation, database provisioning, container deployment,
    /// domain setup, and TLS certificate issuance are in progress.
    /// </summary>
    Provisioning = 3,

    /// <summary>
    /// The app is deployed, hosted, and accessible at its production URL.
    /// Monetization and marketplace features are available (if entitled).
    /// </summary>
    Live = 4,

    /// <summary>
    /// The app is temporarily suspended. Container is stopped,
    /// but data and configuration are preserved. Can be resumed.
    /// </summary>
    Paused = 5,

    /// <summary>
    /// The app is soft-deleted. No runtime, no hosting, no billing.
    /// Data retained for recovery window, then hard-deleted.
    /// </summary>
    Archived = 6
}

/// <summary>
/// Full lifecycle state including computed phase, timestamps, and allowed actions.
/// </summary>
public sealed class AppLifecycleState
{
    public required string AppId { get; init; }

    /// <summary>
    /// The computed canonical phase.
    /// </summary>
    public required AppLifecyclePhase Phase { get; init; }

    /// <summary>
    /// When the current phase started (best estimate).
    /// </summary>
    public DateTime? PhaseSince { get; init; }

    /// <summary>
    /// Actions allowed in the current phase.
    /// </summary>
    public List<string> AllowedActions { get; init; } = new();

    /// <summary>
    /// Actions blocked in the current phase.
    /// </summary>
    public List<string> BlockedActions { get; init; } = new();

    /// <summary>
    /// Underlying status details from each service.
    /// </summary>
    public AppLifecycleDetails Details { get; init; } = new();

    /// <summary>
    /// If the phase involves an error, this contains the error message.
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Underlying status from each service that contributes to the computed phase.
/// </summary>
public sealed class AppLifecycleDetails
{
    /// <summary>
    /// App status from MozaiksApps collection.
    /// </summary>
    public string? AppStatus { get; init; }

    /// <summary>
    /// Build status from AppBuildStatuses collection.
    /// </summary>
    public string? BuildStatus { get; init; }

    /// <summary>
    /// Preview URL if build succeeded.
    /// </summary>
    public string? PreviewUrl { get; init; }

    /// <summary>
    /// Hosted app status from Hosting.API.
    /// </summary>
    public string? HostedStatus { get; init; }

    /// <summary>
    /// Production URL if hosted.
    /// </summary>
    public string? HostedUrl { get; init; }

    /// <summary>
    /// Active provisioning jobs if any.
    /// </summary>
    public List<ProvisioningJobSummary> ProvisioningJobs { get; init; } = new();
}

/// <summary>
/// Summary of a provisioning job.
/// </summary>
public sealed class ProvisioningJobSummary
{
    public string? JobId { get; init; }
    public string? JobType { get; init; }
    public string? Status { get; init; }
    public DateTime? StartedAt { get; init; }
    public string? Error { get; init; }
}
