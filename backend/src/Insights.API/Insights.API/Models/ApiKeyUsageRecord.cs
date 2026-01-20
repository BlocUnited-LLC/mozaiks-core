using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Insights.API.Models;

public sealed class ApiKeyUsageRecord : DocumentBase
{
    [BsonElement("appId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string AppId { get; set; } = string.Empty;

    [BsonElement("lastPingAt")]
    [BsonIgnoreIfNull]
    public DateTime? LastPingAt { get; set; }

    [BsonElement("lastKpiPushAt")]
    [BsonIgnoreIfNull]
    public DateTime? LastKpiPushAt { get; set; }

    [BsonElement("lastEventPushAt")]
    [BsonIgnoreIfNull]
    public DateTime? LastEventPushAt { get; set; }

    [BsonElement("eventsLast24h")]
    public int EventsLast24h { get; set; }

    [BsonElement("sdkVersion")]
    [BsonIgnoreIfNull]
    public string? SdkVersion { get; set; }
}

