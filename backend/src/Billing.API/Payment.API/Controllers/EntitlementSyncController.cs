using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Payment.API.DTOs;
using Payment.API.Infrastructure.Observability;
using Payment.API.Services;
using System.Text.Json;

namespace Payment.API.Controllers;

/// <summary>
/// Controller for receiving entitlement manifest updates from mozaiks-platform.
/// 
/// This is the Core-side endpoint that Platform uses to push updated entitlement
/// manifests after subscription changes. Core then stores these manifests and
/// makes them available to the AI runtime via the ControlPlaneSource.
/// 
/// Security: This endpoint requires platform-level authentication (service-to-service).
/// </summary>
[ApiController]
[Route("api/v1/entitlements")]
[Authorize(Policy = "PlatformService")]
public class EntitlementSyncController : ControllerBase
{
    private readonly IEntitlementManifestStore _manifestStore;
    private readonly StructuredLogEmitter _logs;
    private readonly ICorrelationContextAccessor _correlation;

    public EntitlementSyncController(
        IEntitlementManifestStore manifestStore,
        StructuredLogEmitter logs,
        ICorrelationContextAccessor correlation)
    {
        _manifestStore = manifestStore;
        _logs = logs;
        _correlation = correlation;
    }

    /// <summary>
    /// Receives an updated entitlement manifest from Platform.
    /// Called when a subscription changes (created, upgraded, cancelled, etc.)
    /// </summary>
    [HttpPost("sync")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SyncManifest(
        [FromBody] EntitlementManifestDto manifest,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(manifest.AppId))
        {
            return BadRequest(new { error = "AppId is required" });
        }

        _logs.Info(
            "Entitlement.Manifest.Received",
            new ActorContext { UserId = "platform-service" },
            new EntityContext { AppId = manifest.AppId },
            new
            {
                correlationId = _correlation.CorrelationId,
                tier = manifest.Tier,
                version = manifest.Version,
                featureCount = manifest.Features?.Count ?? 0
            });

        await _manifestStore.StoreAsync(manifest, cancellationToken);

        _logs.Info(
            "Entitlement.Manifest.Stored",
            new ActorContext { UserId = "platform-service" },
            new EntityContext { AppId = manifest.AppId },
            new { correlationId = _correlation.CorrelationId });

        return Ok(new { status = "synced", appId = manifest.AppId, version = manifest.Version });
    }

    /// <summary>
    /// Retrieves the current entitlement manifest for an app.
    /// Used by the AI runtime to fetch entitlements at startup or on-demand.
    /// </summary>
    [HttpGet("{appId}")]
    [AllowAnonymous] // Runtime access - auth happens via app_id validation
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetManifest(
        string appId,
        CancellationToken cancellationToken)
    {
        var manifest = await _manifestStore.GetAsync(appId, cancellationToken);
        
        if (manifest == null)
        {
            // Return default "free" manifest for unknown apps
            // This allows self-hosted/OSS users to run without Platform
            return Ok(CreateDefaultManifest(appId));
        }

        return Ok(manifest);
    }

    /// <summary>
    /// Bulk retrieves manifests for multiple apps.
    /// Used during runtime startup to prefetch entitlements.
    /// </summary>
    [HttpPost("bulk")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetManifestsBulk(
        [FromBody] BulkManifestRequest request,
        CancellationToken cancellationToken)
    {
        var results = new Dictionary<string, EntitlementManifestDto>();

        foreach (var appId in request.AppIds ?? Array.Empty<string>())
        {
            var manifest = await _manifestStore.GetAsync(appId, cancellationToken);
            results[appId] = manifest ?? CreateDefaultManifest(appId);
        }

        return Ok(results);
    }

    /// <summary>
    /// Creates a default "free tier" manifest for self-hosted/OSS mode.
    /// When running without Platform, all apps get unlimited access.
    /// </summary>
    private static EntitlementManifestDto CreateDefaultManifest(string appId)
    {
        return new EntitlementManifestDto
        {
            AppId = appId,
            Tier = "free",
            Features = new List<string> { "*" }, // All features enabled
            TokenBudget = new TokenBudgetDto
            {
                Limit = -1, // Unlimited
                Used = 0,
                ResetAtUtc = null
            },
            Enforcement = "none", // No enforcement in OSS mode
            Version = 0,
            SyncedAtUtc = DateTime.UtcNow
        };
    }
}

/// <summary>
/// DTO for entitlement manifests synced from Platform.
/// Matches the Python EntitlementManifest dataclass in the runtime.
/// </summary>
public class EntitlementManifestDto
{
    public string AppId { get; set; } = string.Empty;
    public string Tier { get; set; } = "free";
    public List<string> Features { get; set; } = new();
    public TokenBudgetDto? TokenBudget { get; set; }
    public string Enforcement { get; set; } = "none";
    public int Version { get; set; }
    public DateTime SyncedAtUtc { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

public class TokenBudgetDto
{
    public long Limit { get; set; }
    public long Used { get; set; }
    public DateTime? ResetAtUtc { get; set; }
}

public class BulkManifestRequest
{
    public string[]? AppIds { get; set; }
}

/// <summary>
/// Interface for storing and retrieving entitlement manifests.
/// Implementations may use MongoDB, Redis, or in-memory storage.
/// </summary>
public interface IEntitlementManifestStore
{
    Task StoreAsync(EntitlementManifestDto manifest, CancellationToken cancellationToken = default);
    Task<EntitlementManifestDto?> GetAsync(string appId, CancellationToken cancellationToken = default);
    Task DeleteAsync(string appId, CancellationToken cancellationToken = default);
}
