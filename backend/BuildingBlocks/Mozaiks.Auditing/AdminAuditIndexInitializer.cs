using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Mozaiks.Auditing;

public sealed class AdminAuditIndexInitializer : IHostedService
{
    private readonly IMongoDatabase _database;
    private readonly IOptions<AdminAuditOptions> _options;
    private readonly ILogger<AdminAuditIndexInitializer> _logger;

    public AdminAuditIndexInitializer(
        IMongoDatabase database,
        IOptions<AdminAuditOptions> options,
        ILogger<AdminAuditIndexInitializer> logger)
    {
        _database = database;
        _options = options;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var collectionName = (_options.Value.CollectionName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(collectionName))
            {
                collectionName = AdminAuditConstants.DefaultCollectionName;
            }

            var collection = _database.GetCollection<BsonDocument>(collectionName);

            var indexModels = new List<CreateIndexModel<BsonDocument>>
            {
                new(
                    Builders<BsonDocument>.IndexKeys.Descending("timestamp"),
                    new CreateIndexOptions { Name = "ix_timestamp_desc" }
                ),
                new(
                    Builders<BsonDocument>.IndexKeys.Ascending("adminUserId").Descending("timestamp"),
                    new CreateIndexOptions { Name = "ix_adminUserId_timestamp_desc" }
                ),
                new(
                    Builders<BsonDocument>.IndexKeys.Ascending("action").Descending("timestamp"),
                    new CreateIndexOptions { Name = "ix_action_timestamp_desc" }
                ),
                new(
                    Builders<BsonDocument>.IndexKeys.Ascending("targetType").Ascending("targetId").Descending("timestamp"),
                    new CreateIndexOptions { Name = "ix_targetType_targetId_timestamp_desc" }
                ),
                new(
                    Builders<BsonDocument>.IndexKeys.Ascending("correlationId").Descending("timestamp"),
                    new CreateIndexOptions { Name = "ix_correlationId_timestamp_desc" }
                )
            };

            var retentionDays = _options.Value.RetentionDays;
            if (retentionDays is > 0)
            {
                indexModels.Add(new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("timestamp"),
                    new CreateIndexOptions
                    {
                        Name = "ix_timestamp_ttl",
                        ExpireAfter = TimeSpan.FromDays(retentionDays.Value)
                    }));
            }

            await collection.Indexes.CreateManyAsync(indexModels, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize admin audit log indexes");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
