using MongoDB.Bson.Serialization.Attributes;

namespace AuthServer.Api.Models
{
    public sealed class PlatformSettingsModel
    {
        public const string PlatformDocumentId = "platform";

        [BsonId]
        public string Id { get; set; } = PlatformDocumentId;

        [BsonElement("enableFunding")]
        public bool EnableFunding { get; set; } = true;

        [BsonElement("enableE2BValidation")]
        public bool EnableE2BValidation { get; set; } = true;

        [BsonElement("defaultPageSize")]
        public int DefaultPageSize { get; set; } = 20;

        [BsonElement("maxPageSize")]
        public int MaxPageSize { get; set; } = 100;

        [BsonElement("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("updatedByUserId")]
        [BsonIgnoreIfNull]
        public string? UpdatedByUserId { get; set; }
    }
}

