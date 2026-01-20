using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace AuthServer.Api.Models;

public sealed class AppBuildStatusModel : DocumentBase
{
    [BsonElement("appId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string AppId { get; set; } = string.Empty;

    [BsonElement("buildId")]
    public string BuildId { get; set; } = string.Empty;

    [BsonElement("eventType")]
    public string EventType { get; set; } = string.Empty;

    [BsonElement("status")]
    public string Status { get; set; } = string.Empty;

    [BsonElement("startedAtUtc")]
    [BsonIgnoreIfNull]
    public DateTime? StartedAtUtc { get; set; }

    [BsonElement("completedAtUtc")]
    [BsonIgnoreIfNull]
    public DateTime? CompletedAtUtc { get; set; }

    [BsonElement("artifacts")]
    public AppBuildArtifactsModel Artifacts { get; set; } = new();

    [BsonElement("error")]
    [BsonIgnoreIfNull]
    public AppBuildErrorModel? Error { get; set; }
}

public sealed class AppBuildArtifactsModel
{
    [BsonElement("previewUrl")]
    [BsonIgnoreIfNull]
    public string? PreviewUrl { get; set; }

    [BsonElement("exportDownloadUrl")]
    [BsonIgnoreIfNull]
    public string? ExportDownloadUrl { get; set; }
}

public sealed class AppBuildErrorModel
{
    [BsonElement("message")]
    public string Message { get; set; } = string.Empty;

    [BsonElement("details")]
    [BsonIgnoreIfNull]
    public string? Details { get; set; }
}

