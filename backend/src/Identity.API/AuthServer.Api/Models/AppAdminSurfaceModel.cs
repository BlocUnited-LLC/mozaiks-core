using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace AuthServer.Api.Models
{
    public sealed class AppAdminSurfaceModel : DocumentBase
    {
        [BsonElement("appId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string AppId { get; set; } = string.Empty;

        [BsonElement("baseUrl")]
        public string BaseUrl { get; set; } = string.Empty;

        [BsonElement("adminKeyProtected")]
        public string AdminKeyProtected { get; set; } = string.Empty;

        [BsonElement("keyVersion")]
        public int KeyVersion { get; set; } = 1;

        [BsonElement("lastRotatedAt")]
        [BsonIgnoreIfNull]
        public DateTime? LastRotatedAt { get; set; }

        [BsonElement("updatedByUserId")]
        [BsonRepresentation(BsonType.ObjectId)]
        [BsonIgnoreIfNull]
        public string? UpdatedByUserId { get; set; }

        [BsonElement("notes")]
        [BsonIgnoreIfNull]
        public string? Notes { get; set; }
    }
}
