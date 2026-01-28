using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Insights.API.Models;

public sealed class TeamMemberReadModel
{
    [BsonElement("appId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string AppId { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; } = string.Empty;

    public int MemberStatus { get; set; } = 1;
}

