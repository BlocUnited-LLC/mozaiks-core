using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Insights.API.Models;

public sealed class MozaiksAppReadModel
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("ownerUserId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string OwnerUserId { get; set; } = string.Empty;
}

