using MongoDB.Bson.Serialization.Attributes;

namespace AuthServer.Api.Models;

public sealed class ExternalLoginModel : DocumentBase
{
    [BsonElement("provider")]
    public required string Provider { get; set; }

    [BsonElement("subject")]
    public required string Subject { get; set; }

    [BsonElement("userId")]
    public required string UserId { get; set; }

    [BsonElement("email")]
    public string? Email { get; set; }
}
