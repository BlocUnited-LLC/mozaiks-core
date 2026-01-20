using Insights.API.Models;
using MongoDB.Driver;

namespace Insights.API.Repository;

public sealed class ApiKeyUsageRepository : IApiKeyUsageRepository
{
    private readonly IMongoCollection<ApiKeyUsageRecord> _collection;

    public ApiKeyUsageRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<ApiKeyUsageRecord>("ApiKeyUsageRecords");
    }

    public async Task<ApiKeyUsageRecord?> GetByAppIdAsync(string appId, CancellationToken cancellationToken)
    {
        return await _collection.Find(x => x.AppId == appId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task UpsertKpiPushAsync(string appId, DateTime nowUtc, string? sdkVersion, CancellationToken cancellationToken)
    {
        var filter = Builders<ApiKeyUsageRecord>.Filter.Eq(x => x.AppId, appId);
        var update = Builders<ApiKeyUsageRecord>.Update
            .SetOnInsert(x => x.AppId, appId)
            .SetOnInsert(x => x.CreatedAt, nowUtc)
            .SetOnInsert(x => x.EventsLast24h, 0)
            .Set(x => x.LastPingAt, nowUtc)
            .Set(x => x.LastKpiPushAt, nowUtc)
            .Set(x => x.SdkVersion, sdkVersion)
            .Set(x => x.UpdatedAt, nowUtc);

        await _collection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, cancellationToken);
    }

    public async Task UpsertEventPushAsync(string appId, DateTime nowUtc, string? sdkVersion, CancellationToken cancellationToken)
    {
        var filter = Builders<ApiKeyUsageRecord>.Filter.Eq(x => x.AppId, appId);
        var update = Builders<ApiKeyUsageRecord>.Update
            .SetOnInsert(x => x.AppId, appId)
            .SetOnInsert(x => x.CreatedAt, nowUtc)
            .SetOnInsert(x => x.EventsLast24h, 0)
            .Set(x => x.LastPingAt, nowUtc)
            .Set(x => x.LastEventPushAt, nowUtc)
            .Set(x => x.SdkVersion, sdkVersion)
            .Set(x => x.UpdatedAt, nowUtc);

        await _collection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, cancellationToken);
    }

    public async Task SetEventsLast24hAsync(string appId, int eventsLast24h, DateTime computedAtUtc, CancellationToken cancellationToken)
    {
        var filter = Builders<ApiKeyUsageRecord>.Filter.Eq(x => x.AppId, appId);
        var update = Builders<ApiKeyUsageRecord>.Update
            .SetOnInsert(x => x.AppId, appId)
            .SetOnInsert(x => x.CreatedAt, computedAtUtc)
            .Set(x => x.EventsLast24h, eventsLast24h)
            .Set(x => x.UpdatedAt, computedAtUtc);

        await _collection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, cancellationToken);
    }
}

