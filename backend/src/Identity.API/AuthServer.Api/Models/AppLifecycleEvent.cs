using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace AuthServer.Api.Models
{
    public class AppLifecycleEvent : DocumentBase
    {
        [BsonElement("appId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string AppId { get; set; } = string.Empty;

        [BsonElement("actorUserId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string ActorUserId { get; set; } = string.Empty;

        [BsonElement("actorRole")]
        public string ActorRole { get; set; } = string.Empty;

        [BsonElement("action")]
        public string Action { get; set; } = string.Empty;

        [BsonElement("reason")]
        [BsonIgnoreIfNull]
        public string? Reason { get; set; }

        [BsonElement("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [BsonElement("correlationId")]
        public string CorrelationId { get; set; } = string.Empty;
    }
}

