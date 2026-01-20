using Insights.API.Models;
using Insights.API.Shared;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Insights.API.Infrastructure;

public sealed class MongoIndexInitializer
{
    private readonly IMongoDatabase _db;
    private readonly InsightsIngestionOptions _options;

    public MongoIndexInitializer(IMongoDatabase db, IOptions<InsightsIngestionOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await EnsureKpiIndexesAsync(cancellationToken);
        await EnsureEventIndexesAsync(cancellationToken);
        await EnsureApiKeyUsageIndexesAsync(cancellationToken);
    }

    private async Task EnsureKpiIndexesAsync(CancellationToken cancellationToken)
    {
        var kpis = _db.GetCollection<InsightKpiPoint>("InsightsKpiPoints");

        var unique = new CreateIndexModel<InsightKpiPoint>(
            Builders<InsightKpiPoint>.IndexKeys
                .Ascending(x => x.AppId)
                .Ascending(x => x.Env)
                .Ascending(x => x.Metric)
                .Ascending(x => x.Bucket)
                .Ascending(x => x.T),
            new CreateIndexOptions { Unique = true, Name = "ux_app_env_metric_bucket_t" });

        await kpis.Indexes.CreateOneAsync(unique, cancellationToken: cancellationToken);

        if (_options.RetentionDays > 0)
        {
            var ttl = new CreateIndexModel<InsightKpiPoint>(
                Builders<InsightKpiPoint>.IndexKeys.Ascending(x => x.T),
                new CreateIndexOptions
                {
                    Name = "ttl_t",
                    ExpireAfter = TimeSpan.FromDays(_options.RetentionDays)
                });

            await kpis.Indexes.CreateOneAsync(ttl, cancellationToken: cancellationToken);
        }
    }

    private async Task EnsureEventIndexesAsync(CancellationToken cancellationToken)
    {
        var events = _db.GetCollection<InsightEvent>("InsightsEvents");

        var unique = new CreateIndexModel<InsightEvent>(
            Builders<InsightEvent>.IndexKeys
                .Ascending(x => x.AppId)
                .Ascending(x => x.Env)
                .Ascending(x => x.EventId),
            new CreateIndexOptions { Unique = true, Name = "ux_app_env_eventId" });

        var query = new CreateIndexModel<InsightEvent>(
            Builders<InsightEvent>.IndexKeys
                .Ascending(x => x.AppId)
                .Ascending(x => x.Env)
                .Descending(x => x.T),
            new CreateIndexOptions { Name = "ix_app_env_t_desc" });

        await events.Indexes.CreateManyAsync(new[] { unique, query }, cancellationToken);

        if (_options.RetentionDays > 0)
        {
            var ttl = new CreateIndexModel<InsightEvent>(
                Builders<InsightEvent>.IndexKeys.Ascending(x => x.T),
                new CreateIndexOptions
                {
                    Name = "ttl_t",
                    ExpireAfter = TimeSpan.FromDays(_options.RetentionDays)
                });

            await events.Indexes.CreateOneAsync(ttl, cancellationToken: cancellationToken);
        }
    }

    private async Task EnsureApiKeyUsageIndexesAsync(CancellationToken cancellationToken)
    {
        var usage = _db.GetCollection<ApiKeyUsageRecord>("ApiKeyUsageRecords");

        var unique = new CreateIndexModel<ApiKeyUsageRecord>(
            Builders<ApiKeyUsageRecord>.IndexKeys.Ascending(x => x.AppId),
            new CreateIndexOptions { Unique = true, Name = "ux_appId" });

        await usage.Indexes.CreateOneAsync(unique, cancellationToken: cancellationToken);
    }
}
