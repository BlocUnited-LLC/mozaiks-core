using Insights.API.Models;
using MongoDB.Driver;

namespace Insights.API.Repository;

public sealed class EventRepository : IEventRepository
{
    private readonly IMongoCollection<InsightEvent> _collection;

    public EventRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<InsightEvent>("InsightsEvents");
    }

    public async Task InsertManyAsync(IEnumerable<InsightEvent> events, CancellationToken cancellationToken)
    {
        var list = events.ToList();
        if (list.Count == 0)
        {
            return;
        }

        try
        {
            await _collection.InsertManyAsync(list, new InsertManyOptions { IsOrdered = false }, cancellationToken);
        }
        catch (MongoBulkWriteException<InsightEvent> ex) when (ex.WriteErrors.All(e => e.Category == ServerErrorCategory.DuplicateKey))
        {
            // idempotency: ignore duplicates
        }
    }

    public async Task<IReadOnlyList<InsightEvent>> GetLatestAsync(
        string appId,
        string env,
        DateTime startUtc,
        DateTime endUtc,
        int limit,
        CancellationToken cancellationToken)
    {
        var resolvedLimit = limit <= 0 ? 50 : Math.Min(limit, 200);

        var filter = Builders<InsightEvent>.Filter.Eq(x => x.AppId, appId)
            & Builders<InsightEvent>.Filter.Eq(x => x.Env, env)
            & Builders<InsightEvent>.Filter.Gte(x => x.T, startUtc)
            & Builders<InsightEvent>.Filter.Lte(x => x.T, endUtc);

        return await _collection.Find(filter)
            .SortByDescending(x => x.T)
            .Limit(resolvedLimit)
            .ToListAsync(cancellationToken);
    }

    public async Task<long> CountSinceAsync(string appId, DateTime sinceUtc, CancellationToken cancellationToken)
    {
        var filter = Builders<InsightEvent>.Filter.Eq(x => x.AppId, appId)
                     & Builders<InsightEvent>.Filter.Gte(x => x.T, sinceUtc);

        return await _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
    }
}
