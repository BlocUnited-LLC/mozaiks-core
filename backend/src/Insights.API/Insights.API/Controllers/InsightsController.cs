using Insights.API.DTOs;
using Insights.API.Models;
using Insights.API.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mozaiks.Auth;

namespace Insights.API.Controllers;

[ApiController]
[Route("api/insights/apps")]
[Authorize]
public sealed class InsightsController : ControllerBase
{
    private static readonly HashSet<string> AllowedRanges = new(StringComparer.OrdinalIgnoreCase)
    {
        "24h",
        "7d",
        "30d"
    };

    private readonly IHostedAppReadRepository _apps;
    private readonly IKpiPointRepository _kpis;
    private readonly IEventRepository _events;
    private readonly IWebHostEnvironment _env;
    private readonly IUserContextAccessor _userContextAccessor;

    public InsightsController(
        IHostedAppReadRepository apps,
        IKpiPointRepository kpis,
        IEventRepository events,
        IWebHostEnvironment env,
        IUserContextAccessor userContextAccessor)
    {
        _apps = apps;
        _kpis = kpis;
        _events = events;
        _env = env;
        _userContextAccessor = userContextAccessor;
    }

    [HttpGet("{appId}/health-summary")]
    public async Task<ActionResult<HealthSummaryResponse>> GetHealthSummary(
        string appId,
        [FromQuery] string? env,
        [FromQuery] string? range,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return BadRequest(new { error = "InvalidAppId" });
        }

        var resolvedRange = string.IsNullOrWhiteSpace(range) ? "24h" : range;
        if (!AllowedRanges.Contains(resolvedRange))
        {
            return BadRequest(new { error = "InvalidRange", allowed = AllowedRanges });
        }

        var hosted = await _apps.GetByAppIdAsync(appId, cancellationToken);
        if (hosted == null)
        {
            return NotFound(new { error = "NotFound" });
        }

        if (!IsPlatformAdmin())
        {
            var userId = GetCurrentUserId();
            if (!string.Equals(hosted.OwnerUserId, userId, StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }
        }

        var computedAt = DateTimeOffset.UtcNow;
        var status = MapHostedAppToHealthStatus(hosted.Status);

        var (startUtc, endUtc) = GetRangeWindowUtc(resolvedRange, computedAt.UtcDateTime);
        var resolvedEnv = ResolveEnv(env);

        // Read latest ingested KPI points for a small fixed set the UI expects.
        // If not ingested yet, keep HasData=false (except uptime fallback from hosting state).
        var defaultBucket = "1m";

        var latestRequests = await _kpis.GetLatestAsync(appId, resolvedEnv, "requests", defaultBucket, startUtc, endUtc, cancellationToken);
        var latestErrorRate = await _kpis.GetLatestAsync(appId, resolvedEnv, "error_rate", defaultBucket, startUtc, endUtc, cancellationToken);
        var latestP95 = await _kpis.GetLatestAsync(appId, resolvedEnv, "p95_latency_ms", defaultBucket, startUtc, endUtc, cancellationToken);
        var latestUptime = await _kpis.GetLatestAsync(appId, resolvedEnv, "uptime", defaultBucket, startUtc, endUtc, cancellationToken);

        var hostingFallbackUptime = status == "Healthy" ? 1.0 : 0.0;
        var uptimeValue = latestUptime?.V ?? hostingFallbackUptime;
        var uptimeHasData = latestUptime != null;

        var kpis = new List<HealthKpi>
        {
            new("requests", latestRequests?.V, latestRequests?.Unit ?? "count", latestRequests != null),
            new("error_rate", latestErrorRate?.V, latestErrorRate?.Unit ?? "ratio", latestErrorRate != null),
            new("p95_latency_ms", latestP95?.V, latestP95?.Unit ?? "ms", latestP95 != null),
            new("uptime", uptimeValue, latestUptime?.Unit ?? "ratio", uptimeHasData || true)
        };

        return Ok(new HealthSummaryResponse(appId, resolvedRange, computedAt, status, kpis));
    }

    [HttpGet("{appId}/kpi-series")]
    public async Task<ActionResult<KpiSeriesResponse>> GetKpiSeries(
        string appId,
        [FromQuery] string? env,
        [FromQuery] string? metric,
        [FromQuery] string? bucket,
        [FromQuery] string? range,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return BadRequest(new { error = "InvalidAppId" });
        }

        var resolvedMetric = string.IsNullOrWhiteSpace(metric) ? "uptime" : metric;
        var resolvedBucket = string.IsNullOrWhiteSpace(bucket) ? "1h" : bucket;
        var resolvedRange = string.IsNullOrWhiteSpace(range) ? "24h" : range;

        if (!AllowedRanges.Contains(resolvedRange))
        {
            return BadRequest(new { error = "InvalidRange", allowed = AllowedRanges });
        }

        var hosted = await _apps.GetByAppIdAsync(appId, cancellationToken);
        if (hosted == null)
        {
            return NotFound(new { error = "NotFound" });
        }

        if (!IsPlatformAdmin())
        {
            var userId = GetCurrentUserId();
            if (!string.Equals(hosted.OwnerUserId, userId, StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }
        }

        var computedAt = DateTimeOffset.UtcNow;
        var (startUtc, endUtc) = GetRangeWindowUtc(resolvedRange, computedAt.UtcDateTime);
        var resolvedEnv = ResolveEnv(env);

        var series = await _kpis.GetSeriesAsync(appId, resolvedEnv, resolvedMetric, resolvedBucket, startUtc, endUtc, cancellationToken);

        var points = series
            .Select(p => new KpiPoint(new DateTimeOffset(p.T, TimeSpan.Zero), p.V))
            .ToList();

        // Fallback: if no ingested uptime yet, return a single point from hosting state.
        if (points.Count == 0 && string.Equals(resolvedMetric, "uptime", StringComparison.OrdinalIgnoreCase))
        {
            var status = MapHostedAppToHealthStatus(hosted.Status);
            var uptimeValue = status == "Healthy" ? 1.0 : 0.0;
            points.Add(new KpiPoint(DateTimeOffset.UtcNow, uptimeValue));
        }

        return Ok(new KpiSeriesResponse(appId, resolvedMetric, resolvedBucket, points));
    }

    [HttpGet("{appId}/events")]
    public async Task<ActionResult<EventsResponse>> GetEvents(
        string appId,
        [FromQuery] string? env,
        [FromQuery] int? limit,
        [FromQuery] string? range,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return BadRequest(new { error = "InvalidAppId" });
        }

        var resolvedRange = string.IsNullOrWhiteSpace(range) ? "24h" : range;
        if (!AllowedRanges.Contains(resolvedRange))
        {
            return BadRequest(new { error = "InvalidRange", allowed = AllowedRanges });
        }

        var hosted = await _apps.GetByAppIdAsync(appId, cancellationToken);
        if (hosted == null)
        {
            return NotFound(new { error = "NotFound" });
        }

        if (!IsPlatformAdmin())
        {
            var userId = GetCurrentUserId();
            if (!string.Equals(hosted.OwnerUserId, userId, StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }
        }

        var computedAt = DateTimeOffset.UtcNow;
        var (startUtc, endUtc) = GetRangeWindowUtc(resolvedRange, computedAt.UtcDateTime);
        var resolvedEnv = ResolveEnv(env);
        var resolvedLimit = limit ?? 50;

        var events = await _events.GetLatestAsync(appId, resolvedEnv, startUtc, endUtc, resolvedLimit, cancellationToken);

        var dto = events
            .Select(e => new AppEvent(
                e.Type,
                e.Message ?? string.Empty,
                new DateTimeOffset(e.T, TimeSpan.Zero)))
            .ToList();

        return Ok(new EventsResponse(appId, dto));
    }

    private string ResolveEnv(string? env)
    {
        if (!string.IsNullOrWhiteSpace(env))
        {
            return env.Trim();
        }

        return _env.IsDevelopment() ? "development" : "production";
    }

    private static (DateTime startUtc, DateTime endUtc) GetRangeWindowUtc(string range, DateTime nowUtc)
    {
        var end = nowUtc;
        var start = range switch
        {
            "24h" => end.AddHours(-24),
            "7d" => end.AddDays(-7),
            "30d" => end.AddDays(-30),
            _ => end.AddHours(-24)
        };

        return (start, end);
    }

    private static string MapHostedAppToHealthStatus(HostedAppStatus status)
        => status switch
        {
            HostedAppStatus.Active => "Healthy",
            HostedAppStatus.Pending => "Degraded",
            HostedAppStatus.Provisioning => "Degraded",
            HostedAppStatus.Failed => "Down",
            HostedAppStatus.Suspended => "Down",
            _ => "Degraded"
        };

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

    private string GetCurrentUserId()
        => _userContextAccessor.GetUser(User)?.UserId ?? "unknown";
}
