using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace AuthServer.Api.Models
{
    public class DocumentBase
    {
        //[JsonIgnore]
        [BsonRepresentation(BsonType.ObjectId)]
        [BsonElement("_id")]
        //[BsonId(IdGenerator = typeof(StringObjectIdGenerator))]

        public string? Id { get; set; } = MongoDB.Bson.ObjectId.GenerateNewId().ToString();
        //[BsonId]
        //[BsonRepresentation(BsonType.ObjectId)]
        //[JsonIgnore]
        //public string Id { get; set; } = System.Guid.NewGuid().ToString("D");

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [BsonElement("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
