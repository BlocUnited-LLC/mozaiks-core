using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Insights.API.Models;

public class DocumentBase
{
    [BsonRepresentation(BsonType.ObjectId)]
    [BsonElement("_id")]
    public string? Id { get; set; } = ObjectId.GenerateNewId().ToString();

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
