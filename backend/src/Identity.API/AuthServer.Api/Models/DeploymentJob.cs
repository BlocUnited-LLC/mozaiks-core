using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace AuthServer.Api.Models;

public enum DeploymentStatus
{
    Queued,
    Running,
    Completed,
    Failed
}

public sealed class DeploymentJob : DocumentBase
{
    [BsonRepresentation(BsonType.ObjectId)]
    public string AppId { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; } = string.Empty;

    public string AppName { get; set; } = string.Empty;

    public string RepoName { get; set; } = string.Empty;

    public string Branch { get; set; } = "main";

    public string CommitMessage { get; set; } = "Initial app generation from Mozaiks";

    public string? BundleUrl { get; set; }

    public string BundleStoragePath { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.String)]
    public DeploymentStatus Status { get; set; } = DeploymentStatus.Queued;

    public string? RepoUrl { get; set; }

    public string? RepoFullName { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public int Attempt { get; set; } = 0;

    public string CorrelationId { get; set; } = string.Empty;
}

