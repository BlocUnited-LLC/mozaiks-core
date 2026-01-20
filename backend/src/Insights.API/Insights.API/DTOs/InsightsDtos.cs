namespace Insights.API.DTOs;

public sealed record HealthKpi(string Name, double? Value, string Unit, bool HasData);

public sealed record HealthSummaryResponse(
    string AppId,
    string Range,
    DateTimeOffset ComputedAtUtc,
    string Status,
    IReadOnlyList<HealthKpi> Kpis);

public sealed record KpiPoint(DateTimeOffset T, double V);

public sealed record KpiSeriesResponse(
    string AppId,
    string Metric,
    string Bucket,
    IReadOnlyList<KpiPoint> Points);

public sealed record AppEvent(string Type, string Message, DateTimeOffset T);

public sealed record EventsResponse(string AppId, IReadOnlyList<AppEvent> Events);

public sealed record SdkStatusResponse(
    bool Connected,
    DateTime? LastPingAt,
    DateTime? LastKpiPushAt,
    DateTime? LastEventPushAt,
    int EventsLast24h,
    string? SdkVersion);
