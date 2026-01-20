using Insights.API.DTOs;
using Insights.API.Models;
using Insights.API.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mozaiks.Auth;

namespace Insights.API.Controllers;

[ApiController]
[Route("api/internal/insights/ingest")]
[Authorize(Policy = MozaiksAuthDefaults.InternalServicePolicy)]
public sealed class InternalIngestController : ControllerBase
{
    private const string AppIdHeader = "X-Mozaiks-App-Id";
    private const string SdkVersionHeader = "X-Mozaiks-Sdk-Version";

    private readonly IKpiPointRepository _kpis;
    private readonly IEventRepository _events;
    private readonly IApiKeyUsageRepository _usage;
    private readonly IWebHostEnvironment _env;

    public InternalIngestController(
        IKpiPointRepository kpis,
        IEventRepository events,
        IApiKeyUsageRepository usage,
        IWebHostEnvironment env)
    {
        _kpis = kpis;
        _events = events;
        _usage = usage;
        _env = env;
    }

    [HttpPost("kpis")]
    public async Task<IActionResult> IngestKpis([FromBody] IngestKpisRequest request, CancellationToken cancellationToken)
    {
        var correlationId = GetOrCreateCorrelationId();
        Response.Headers["x-correlation-id"] = correlationId;

        var resolved = ResolveAppId(request.AppId);
        if (resolved.ErrorResult is not null)
        {
            return resolved.ErrorResult;
        }

        var appId = resolved.AppId!;

        if (request.Points == null || request.Points.Count == 0)
        {
            return BadRequest(new { error = "NoPoints" });
        }

        var env = string.IsNullOrWhiteSpace(request.Env) ? GetDefaultEnv() : request.Env.Trim();
        var bucket = string.IsNullOrWhiteSpace(request.Bucket) ? "1m" : request.Bucket.Trim();

        var now = DateTime.UtcNow;

        var docs = request.Points
            .Where(p => !string.IsNullOrWhiteSpace(p.Metric))
            .Select(p => new InsightKpiPoint
            {
                AppId = appId,
                Env = env,
                Metric = p.Metric.Trim(),
                Bucket = bucket,
                T = p.T.UtcDateTime,
                V = p.V,
                Unit = p.Unit?.Trim() ?? string.Empty,
                Tags = request.Tags?.ToDictionary(k => k.Key, v => v.Value),
                CreatedAt = now,
                UpdatedAt = now
            })
            .ToList();

        await _kpis.InsertManyAsync(docs, cancellationToken);
        await _usage.UpsertKpiPushAsync(appId, now, GetSdkVersion(), cancellationToken);

        return Accepted(new { inserted = docs.Count });
    }

    [HttpPost("events")]
    public async Task<IActionResult> IngestEvents([FromBody] IngestEventsRequest request, CancellationToken cancellationToken)
    {
        var correlationId = GetOrCreateCorrelationId();
        Response.Headers["x-correlation-id"] = correlationId;

        var resolved = ResolveAppId(request.AppId);
        if (resolved.ErrorResult is not null)
        {
            return resolved.ErrorResult;
        }

        var appId = resolved.AppId!;

        if (request.Events == null || request.Events.Count == 0)
        {
            return BadRequest(new { error = "NoEvents" });
        }

        var env = string.IsNullOrWhiteSpace(request.Env) ? GetDefaultEnv() : request.Env.Trim();
        var now = DateTime.UtcNow;

        var docs = request.Events
            .Where(e => !string.IsNullOrWhiteSpace(e.EventId) && !string.IsNullOrWhiteSpace(e.Type))
            .Select(e => new InsightEvent
            {
                AppId = appId,
                Env = env,
                EventId = e.EventId.Trim(),
                T = e.T.UtcDateTime,
                Type = e.Type.Trim(),
                Severity = e.Severity?.Trim(),
                Message = e.Message,
                Data = e.Data?.ToDictionary(k => k.Key, v => v.Value),
                CreatedAt = now,
                UpdatedAt = now
            })
            .ToList();

        await _events.InsertManyAsync(docs, cancellationToken);
        await _usage.UpsertEventPushAsync(appId, now, GetSdkVersion(), cancellationToken);

        return Accepted(new { inserted = docs.Count });
    }

    private (string? AppId, IActionResult? ErrorResult) ResolveAppId(string? bodyAppId)
    {
        var headerAppId = (Request.Headers[AppIdHeader].ToString() ?? string.Empty).Trim();
        var resolvedAppId = (bodyAppId ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(resolvedAppId))
        {
            resolvedAppId = headerAppId;
        }

        if (string.IsNullOrWhiteSpace(resolvedAppId))
        {
            return (null, BadRequest(new { error = "InvalidAppId" }));
        }

        if (!string.IsNullOrWhiteSpace(headerAppId)
            && !string.Equals(headerAppId, resolvedAppId, StringComparison.OrdinalIgnoreCase))
        {
            return (null, BadRequest(new { error = "AppIdMismatch" }));
        }

        return (resolvedAppId, null);
    }

    private string GetDefaultEnv()
        => _env.IsDevelopment() ? "development" : "production";

    private string GetOrCreateCorrelationId()
    {
        var header = Request.Headers["x-correlation-id"].ToString();
        return string.IsNullOrWhiteSpace(header) ? Guid.NewGuid().ToString() : header;
    }

    private string? GetSdkVersion()
    {
        var version = (Request.Headers[SdkVersionHeader].ToString() ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        return version.Length > 64 ? version[..64] : version;
    }
}

