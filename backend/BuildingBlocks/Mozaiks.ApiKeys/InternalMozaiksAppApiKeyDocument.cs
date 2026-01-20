using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Mozaiks.ApiKeys;

internal sealed class InternalMozaiksAppApiKeyDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("ownerUserId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string OwnerUserId { get; set; } = string.Empty;

    [BsonElement("apiKeyHash")]
    [BsonIgnoreIfNull]
    public string? ApiKeyHash { get; set; }

    [BsonElement("apiKeyLastUsedAt")]
    [BsonIgnoreIfNull]
    public DateTime? ApiKeyLastUsedAt { get; set; }
}

