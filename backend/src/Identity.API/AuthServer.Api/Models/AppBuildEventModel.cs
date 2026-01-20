using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace AuthServer.Api.Models;

public sealed class AppBuildEventModel : DocumentBase
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

    [BsonElement("occurredAtUtc")]
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    [BsonElement("artifacts")]
    [BsonIgnoreIfNull]
    public AppBuildArtifactsModel? Artifacts { get; set; }

    [BsonElement("error")]
    [BsonIgnoreIfNull]
    public AppBuildErrorModel? Error { get; set; }
}

