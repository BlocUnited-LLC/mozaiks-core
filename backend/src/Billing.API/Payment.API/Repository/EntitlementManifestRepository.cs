using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Payment.API.Controllers;
using Payment.API.Infrastructure.Observability;

namespace Payment.API.Repository;

/// <summary>
/// MongoDB implementation of IEntitlementManifestStore.
/// Stores entitlement manifests synced from mozaiks-platform.
/// </summary>
public class EntitlementManifestRepository : IEntitlementManifestStore
{
    private readonly IMongoCollection<EntitlementManifestDocument> _collection;
    private readonly StructuredLogEmitter _logs;

    public EntitlementManifestRepository(IMongoDatabase database, StructuredLogEmitter logs)
    {
        _collection = database.GetCollection<EntitlementManifestDocument>("entitlement_manifests");
        _logs = logs;
        
        // Ensure indexes exist
        var indexKeysDefinition = Builders<EntitlementManifestDocument>.IndexKeys.Ascending(x => x.AppId);
        var indexOptions = new CreateIndexOptions { Unique = true };
        var indexModel = new CreateIndexModel<EntitlementManifestDocument>(indexKeysDefinition, indexOptions);
        _collection.Indexes.CreateOne(indexModel);
    }

    public async Task StoreAsync(EntitlementManifestDto manifest, CancellationToken cancellationToken = default)
    {
        var document = new EntitlementManifestDocument
        {
            AppId = manifest.AppId,
            Tier = manifest.Tier,
            Features = manifest.Features,
            TokenBudget = manifest.TokenBudget != null
                ? new TokenBudgetDocument
                {
                    Limit = manifest.TokenBudget.Limit,
                    Used = manifest.TokenBudget.Used,
                    ResetAtUtc = manifest.TokenBudget.ResetAtUtc
                }
                : null,
            Enforcement = manifest.Enforcement,
            Version = manifest.Version,
            SyncedAtUtc = manifest.SyncedAtUtc,
            Metadata = manifest.Metadata,
            UpdatedAtUtc = DateTime.UtcNow
        };

        var filter = Builders<EntitlementManifestDocument>.Filter.Eq(x => x.AppId, manifest.AppId);
        var options = new ReplaceOptions { IsUpsert = true };

        await _collection.ReplaceOneAsync(filter, document, options, cancellationToken);

        _logs.Debug(
            "Entitlement.Manifest.Persisted",
            new ActorContext { UserId = "system" },
            new EntityContext { AppId = manifest.AppId },
            new { version = manifest.Version, tier = manifest.Tier });
    }

    public async Task<EntitlementManifestDto?> GetAsync(string appId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<EntitlementManifestDocument>.Filter.Eq(x => x.AppId, appId);
        var document = await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);

        if (document == null)
        {
            return null;
        }

        return new EntitlementManifestDto
        {
            AppId = document.AppId,
            Tier = document.Tier,
            Features = document.Features,
            TokenBudget = document.TokenBudget != null
                ? new TokenBudgetDto
                {
                    Limit = document.TokenBudget.Limit,
                    Used = document.TokenBudget.Used,
                    ResetAtUtc = document.TokenBudget.ResetAtUtc
                }
                : null,
            Enforcement = document.Enforcement,
            Version = document.Version,
            SyncedAtUtc = document.SyncedAtUtc,
            Metadata = document.Metadata
        };
    }

    public async Task DeleteAsync(string appId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<EntitlementManifestDocument>.Filter.Eq(x => x.AppId, appId);
        await _collection.DeleteOneAsync(filter, cancellationToken);

        _logs.Debug(
            "Entitlement.Manifest.Deleted",
            new ActorContext { UserId = "system" },
            new EntityContext { AppId = appId },
            new { });
    }
}

/// <summary>
/// MongoDB document for storing entitlement manifests.
/// </summary>
public class EntitlementManifestDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("app_id")]
    public string AppId { get; set; } = string.Empty;

    [BsonElement("tier")]
    public string Tier { get; set; } = "free";

    [BsonElement("features")]
    public List<string> Features { get; set; } = new();

    [BsonElement("token_budget")]
    public TokenBudgetDocument? TokenBudget { get; set; }

    [BsonElement("enforcement")]
    public string Enforcement { get; set; } = "none";

    [BsonElement("version")]
    public int Version { get; set; }

    [BsonElement("synced_at_utc")]
    public DateTime SyncedAtUtc { get; set; }

    [BsonElement("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }

    [BsonElement("updated_at_utc")]
    public DateTime UpdatedAtUtc { get; set; }
}

public class TokenBudgetDocument
{
    [BsonElement("limit")]
    public long Limit { get; set; }

    [BsonElement("used")]
    public long Used { get; set; }

    [BsonElement("reset_at_utc")]
    public DateTime? ResetAtUtc { get; set; }
}
