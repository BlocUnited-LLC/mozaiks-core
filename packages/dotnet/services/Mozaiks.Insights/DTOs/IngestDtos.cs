namespace Insights.API.DTOs;

public sealed record IngestKpisRequest(
    string AppId,
    string? Env,
    DateTimeOffset? SentAtUtc,
    string? Bucket,
    IReadOnlyList<IngestKpiPoint> Points,
    IReadOnlyDictionary<string, string>? Tags,
    string? DedupeKey);

public sealed record IngestKpiPoint(
    string Metric,
    DateTimeOffset T,
    double V,
    string? Unit);

public sealed record IngestEventsRequest(
    string AppId,
    string? Env,
    DateTimeOffset? SentAtUtc,
    IReadOnlyList<IngestEvent> Events);

public sealed record IngestEvent(
    string EventId,
    DateTimeOffset T,
    string Type,
    string? Severity,
    string? Message,
    IReadOnlyDictionary<string, object>? Data);
