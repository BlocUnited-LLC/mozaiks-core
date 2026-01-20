using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace Notification.API.Models
{
    public class DocumentBase
    {
        [BsonRepresentation(BsonType.ObjectId)]
        [BsonElement("_id")]

        public string? Id { get; set; } = MongoDB.Bson.ObjectId.GenerateNewId().ToString();
        
        [BsonElement("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [BsonElement("UpdatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
