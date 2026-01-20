using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace AuthServer.Api.Models
{
    /// <summary>
    /// CONTROL PLANE ENTITY: App Registry
    /// 
    /// This is the authoritative record for a Mozaiks app.
    /// Collection: MozaiksApps (MongoDB)
    /// Source of Truth: This service (AuthServer.Api)
    /// 
    /// Related entities:
    /// - HostedApps (Hosting.API): Deployment state, references AppId
    /// - FundingRounds (Governance.API): Funding campaigns, references AppId
    /// - Teams: Team members, references AppId
    /// - Transactions: App subscriptions, references Metadata.AppId
    /// 
    /// Lifecycle: Draft → Built → Deployed → (Paused ↔ Active) → Deleted
    /// 
    /// MozaiksCore instances are identified by this _id (appId).
    /// GitHub repos are referenced (not owned) via GitHubRepoUrl.
    /// </summary>
    public class MozaiksAppModel : DocumentBase
    {
        [BsonElement("name")]
        public required string Name { get; set; }

        [BsonElement("ownerUserId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string OwnerUserId { get; set; } = string.Empty;

        [BsonElement("industry")]
        [BsonIgnoreIfNull]
        public string? Industry { get; set; }

        [BsonElement("logoUrl")]
        [BsonIgnoreIfNull]
        public string? LogoUrl { get; set; }

        [BsonElement("description")]
        public string Description { get; set; } = string.Empty;

        public string GithubId { get; set; } = string.Empty;
        public string LinkedInId { get; set; } = string.Empty;
        public string FacebookId { get; set; } = string.Empty;
        public string[] TeamMembers { get; set; } = Array.Empty<string>();

        [BsonElement("installedPlugins")]
        public List<string> InstalledPlugins { get; set; } = new();

        public int AvailableTokens { get; set; }
        public int DevelopmentRoute { get; set; }
        public int UsersExperienceClassification { get; set; }
        public bool? IsPublicMozaik { get; set; }

        [BsonElement("featureFlags")]
        public Dictionary<string, bool>? FeatureFlags { get; set; }

        [BsonElement("apiKeyHash")]
        [BsonIgnoreIfNull]
        public string? ApiKeyHash { get; set; }

        [BsonElement("apiKeyPrefix")]
        [BsonIgnoreIfNull]
        public string? ApiKeyPrefix { get; set; }

        [BsonElement("apiKeyCreatedAt")]
        [BsonIgnoreIfNull]
        public DateTime? ApiKeyCreatedAt { get; set; }

        [BsonElement("apiKeyLastUsedAt")]
        [BsonIgnoreIfNull]
        public DateTime? ApiKeyLastUsedAt { get; set; }

        [BsonElement("apiKeyVersion")]
        public int ApiKeyVersion { get; set; } = 0;

        [BsonElement("gitHubRepoUrl")]
        [BsonIgnoreIfNull]
        public string? GitHubRepoUrl { get; set; }

        [BsonElement("gitHubRepoFullName")]
        [BsonIgnoreIfNull]
        public string? GitHubRepoFullName { get; set; }

        [BsonElement("deployedAt")]
        [BsonIgnoreIfNull]
        public DateTime? DeployedAt { get; set; }

        [BsonElement("databaseName")]
        [BsonIgnoreIfNull]
        public string? DatabaseName { get; set; }

        [BsonElement("databaseProvisionedAt")]
        [BsonIgnoreIfNull]
        public DateTime? DatabaseProvisionedAt { get; set; }

        [BsonElement("status")]
        public AppStatus Status { get; set; } = AppStatus.Draft;

        [BsonElement("pausedAt")]
        [BsonIgnoreIfNull]
        public DateTime? PausedAt { get; set; }

        [BsonElement("resumedAt")]
        [BsonIgnoreIfNull]
        public DateTime? ResumedAt { get; set; }

        [BsonElement("isDeleted")]
        public bool IsDeleted { get; set; }

        [BsonElement("deletedAt")]
        [BsonIgnoreIfNull]
        public DateTime? DeletedAt { get; set; }

        [BsonElement("hardDeleteAt")]
        [BsonIgnoreIfNull]
        public DateTime? HardDeleteAt { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
