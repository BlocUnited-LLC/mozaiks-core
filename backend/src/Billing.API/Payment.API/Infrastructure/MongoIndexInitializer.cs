using MongoDB.Driver;
using Payment.API.Models;

namespace Payment.API.Infrastructure;

public sealed class MongoIndexInitializer
{
    private readonly IMongoDatabase _db;

    public MongoIndexInitializer(IMongoDatabase db)
    {
        _db = db;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await EnsureEconomicEventIndexesAsync(cancellationToken);
    }

    private async Task EnsureEconomicEventIndexesAsync(CancellationToken cancellationToken)
    {
        var events = _db.GetCollection<EconomicEventDocument>("EconomicEvents");

        var unique = new CreateIndexModel<EconomicEventDocument>(
            Builders<EconomicEventDocument>.IndexKeys
                .Ascending(x => x.AppId)
                .Ascending(x => x.Env)
                .Ascending(x => x.EventId),
            new CreateIndexOptions { Unique = true, Name = "ux_app_env_eventId" });

        var byTime = new CreateIndexModel<EconomicEventDocument>(
            Builders<EconomicEventDocument>.IndexKeys
                .Ascending(x => x.AppId)
                .Ascending(x => x.Env)
                .Descending(x => x.OccurredAtUtc),
            new CreateIndexOptions { Name = "ix_app_env_occurredAt_desc" });

        var byCampaign = new CreateIndexModel<EconomicEventDocument>(
            Builders<EconomicEventDocument>.IndexKeys
                .Ascending(x => x.AppId)
                .Ascending(x => x.Env)
                .Ascending(x => x.CampaignId)
                .Descending(x => x.OccurredAtUtc),
            new CreateIndexOptions { Name = "ix_app_env_campaign_occurredAt_desc" });

        await events.Indexes.CreateManyAsync(new[] { unique, byTime, byCampaign }, cancellationToken);
    }
}

