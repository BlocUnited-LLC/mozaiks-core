using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Insights.API.Models;

public enum HostedAppStatus
{
    Pending,
    Provisioning,
    Active,
    Failed,
    Suspended
}

public sealed class HostedApp : DocumentBase
{
    [BsonRepresentation(BsonType.ObjectId)]
    public string AppId { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.ObjectId)]
    public string OwnerUserId { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.String)]
    public HostedAppStatus Status { get; set; } = HostedAppStatus.Pending;
}
