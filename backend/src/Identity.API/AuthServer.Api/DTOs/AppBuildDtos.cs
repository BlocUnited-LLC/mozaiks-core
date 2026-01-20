using System.Text.Json.Serialization;

namespace AuthServer.Api.DTOs;

public sealed class AppBuildEventRequest
{
    [JsonPropertyName("event_type")]
    public string EventType { get; set; } = string.Empty;

    public string? AppId { get; set; }

    public string? BuildId { get; set; }

    public string? Status { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public AppBuildArtifactsDto? Artifacts { get; set; }

    public AppBuildErrorDto? Error { get; set; }
}

public sealed class AppBuildArtifactsDto
{
    public string? PreviewUrl { get; set; }
    public string? ExportDownloadUrl { get; set; }
}

public sealed class AppBuildErrorDto
{
    public string? Message { get; set; }
    public string? Details { get; set; }
}

public sealed record AppBuildLatestResponse(
    string AppId,
    string? BuildId,
    string Status,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    AppBuildArtifactsDto Artifacts,
    AppBuildErrorDto? Error,
    DateTimeOffset UpdatedAtUtc);

