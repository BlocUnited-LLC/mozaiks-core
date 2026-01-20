using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace AuthServer.Api.Models
{
    public sealed class AppModuleProxyAuditEvent : DocumentBase
    {
        [BsonElement("appId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string AppId { get; set; } = string.Empty;

        [BsonElement("moduleId")]
        public string ModuleId { get; set; } = string.Empty;

        [BsonElement("actionId")]
        [BsonIgnoreIfNull]
        public string? ActionId { get; set; }

        [BsonElement("operation")]
        public string Operation { get; set; } = string.Empty; // "settings_update" | "action_invoke"

        [BsonElement("actorUserId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string ActorUserId { get; set; } = string.Empty;

        [BsonElement("actorRole")]
        public string ActorRole { get; set; } = string.Empty; // "creator" | "team" | "admin"

        [BsonElement("correlationId")]
        public string CorrelationId { get; set; } = string.Empty;

        [BsonElement("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [BsonElement("success")]
        public bool Success { get; set; }

        [BsonElement("statusCode")]
        public int StatusCode { get; set; }

        [BsonElement("path")]
        public string Path { get; set; } = string.Empty;

        [BsonElement("requestBytes")]
        public long RequestBytes { get; set; }

        [BsonElement("responseBytes")]
        public long ResponseBytes { get; set; }

        [BsonElement("errorMessage")]
        [BsonIgnoreIfNull]
        public string? ErrorMessage { get; set; }
    }
}

