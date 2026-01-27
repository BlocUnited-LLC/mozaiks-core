using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Payment.API.Infrastructure.Observability;
using Payment.API.Repository;

namespace Payment.API.Controllers;

/// <summary>
/// Controller for receiving token usage events from the AI runtime.
/// 
/// This is the Core-side endpoint that the AI runtime uses to report token
/// consumption. Core stores these events and can forward them to Platform
/// for usage-based billing.
/// 
/// Flow:
/// 1. AI runtime executes workflow, tracks token usage via TokenBudgetTracker
/// 2. Runtime sends usage event to this endpoint
/// 3. Core stores event and updates local manifest's "used" counter
/// 4. (If Platform mode) Core forwards event to Platform's UsageBillingPipeline
/// </summary>
[ApiController]
[Route("api/v1/usage")]
public class UsageEventController : ControllerBase
{
    private readonly IUsageEventStore _usageStore;
    private readonly IEntitlementManifestStore _manifestStore;
    private readonly StructuredLogEmitter _logs;
    private readonly ObservabilityMetrics _metrics;
    private readonly ICorrelationContextAccessor _correlation;
    private readonly IConfiguration _configuration;

    public UsageEventController(
        IUsageEventStore usageStore,
        IEntitlementManifestStore manifestStore,
        StructuredLogEmitter logs,
        ObservabilityMetrics metrics,
        ICorrelationContextAccessor correlation,
        IConfiguration configuration)
    {
        _usageStore = usageStore;
        _manifestStore = manifestStore;
        _logs = logs;
        _metrics = metrics;
        _correlation = correlation;
        _configuration = configuration;
    }

    /// <summary>
    /// Records a token usage event from the AI runtime.
    /// </summary>
    [HttpPost("tokens")]
    [AllowAnonymous] // Runtime reports usage - auth via app_id + internal network
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RecordTokenUsage(
        [FromBody] TokenUsageEventDto usageEvent,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(usageEvent.AppId))
        {
            return BadRequest(new { error = "AppId is required" });
        }

        if (usageEvent.TokensUsed <= 0)
        {
            return BadRequest(new { error = "TokensUsed must be positive" });
        }

        // Store the usage event
        await _usageStore.RecordAsync(usageEvent, cancellationToken);

        // Update local manifest's used counter
        var manifest = await _manifestStore.GetAsync(usageEvent.AppId, cancellationToken);
        if (manifest?.TokenBudget != null)
        {
            manifest.TokenBudget.Used += usageEvent.TokensUsed;
            await _manifestStore.StoreAsync(manifest, cancellationToken);
        }

        // Record metrics
        _metrics.RecordTokenUsage(usageEvent.AppId, usageEvent.TokensUsed);

        _logs.Debug(
            "Usage.TokenEvent.Recorded",
            new ActorContext { UserId = usageEvent.UserId ?? "anonymous" },
            new EntityContext { AppId = usageEvent.AppId },
            new
            {
                correlationId = _correlation.CorrelationId,
                tokens = usageEvent.TokensUsed,
                model = usageEvent.ModelId,
                workflowId = usageEvent.WorkflowId
            });

        // If Platform mode is enabled, forward to Platform's billing pipeline
        var platformEnabled = _configuration.GetValue<bool>("Platform:Enabled");
        if (platformEnabled)
        {
            // TODO: Forward to Platform via message queue or HTTP
            // This will be implemented when the Platform-side UsageBillingPipeline is ready
            _logs.Debug(
                "Usage.TokenEvent.ForwardToPlatform",
                new ActorContext { UserId = usageEvent.UserId ?? "anonymous" },
                new EntityContext { AppId = usageEvent.AppId },
                new { correlationId = _correlation.CorrelationId });
        }

        return Accepted(new
        {
            status = "recorded",
            eventId = usageEvent.EventId,
            appId = usageEvent.AppId
        });
    }

    /// <summary>
    /// Gets usage summary for an app within a time range.
    /// </summary>
    [HttpGet("{appId}/summary")]
    [Authorize] // Requires user auth
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsageSummary(
        string appId,
        [FromQuery] DateTime? startUtc,
        [FromQuery] DateTime? endUtc,
        CancellationToken cancellationToken)
    {
        var start = startUtc ?? DateTime.UtcNow.AddDays(-30);
        var end = endUtc ?? DateTime.UtcNow;

        var summary = await _usageStore.GetSummaryAsync(appId, start, end, cancellationToken);

        return Ok(summary);
    }

    /// <summary>
    /// Gets detailed usage events for an app (for debugging/audit).
    /// </summary>
    [HttpGet("{appId}/events")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsageEvents(
        string appId,
        [FromQuery] DateTime? startUtc,
        [FromQuery] DateTime? endUtc,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var start = startUtc ?? DateTime.UtcNow.AddDays(-7);
        var end = endUtc ?? DateTime.UtcNow;

        var events = await _usageStore.GetEventsAsync(appId, start, end, limit, cancellationToken);

        return Ok(new { events, count = events.Count });
    }
}

/// <summary>
/// DTO for token usage events from the AI runtime.
/// Matches the Python TokenUsageEvent in the entitlements package.
/// </summary>
public class TokenUsageEventDto
{
    public string EventId { get; set; } = Guid.NewGuid().ToString();
    public string AppId { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? WorkflowId { get; set; }
    public string? SessionId { get; set; }
    public string? ModelId { get; set; }
    public long TokensUsed { get; set; }
    public long? InputTokens { get; set; }
    public long? OutputTokens { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Usage summary returned by the summary endpoint.
/// </summary>
public class UsageSummaryDto
{
    public string AppId { get; set; } = string.Empty;
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public long TotalTokens { get; set; }
    public long TotalInputTokens { get; set; }
    public long TotalOutputTokens { get; set; }
    public int TotalEvents { get; set; }
    public Dictionary<string, long> TokensByModel { get; set; } = new();
    public Dictionary<string, long> TokensByWorkflow { get; set; } = new();
}

/// <summary>
/// Interface for storing and querying usage events.
/// </summary>
public interface IUsageEventStore
{
    Task RecordAsync(TokenUsageEventDto usageEvent, CancellationToken cancellationToken = default);
    Task<UsageSummaryDto> GetSummaryAsync(string appId, DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken = default);
    Task<List<TokenUsageEventDto>> GetEventsAsync(string appId, DateTime startUtc, DateTime endUtc, int limit, CancellationToken cancellationToken = default);
}
