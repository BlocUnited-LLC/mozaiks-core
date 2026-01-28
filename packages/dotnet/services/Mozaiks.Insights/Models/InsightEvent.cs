using MongoDB.Bson.Serialization.Attributes;

namespace Insights.API.Models;

public sealed class InsightEvent : DocumentBase
{
    [BsonElement("appId")]
    public string AppId { get; set; } = string.Empty;

    [BsonElement("env")]
    public string Env { get; set; } = string.Empty;

    [BsonElement("eventId")]
    public string EventId { get; set; } = string.Empty;

    [BsonElement("t")]
    public DateTime T { get; set; }

    [BsonElement("type")]
    public string Type { get; set; } = string.Empty;

    [BsonElement("severity")]
    public string? Severity { get; set; }

    [BsonElement("message")]
    public string? Message { get; set; }

    [BsonElement("data")]
    public Dictionary<string, object>? Data { get; set; }
}
