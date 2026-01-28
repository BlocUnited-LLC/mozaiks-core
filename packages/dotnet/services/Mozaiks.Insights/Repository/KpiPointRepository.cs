using Insights.API.Models;
using MongoDB.Driver;

namespace Insights.API.Repository;

public sealed class KpiPointRepository : IKpiPointRepository
{
    private readonly IMongoCollection<InsightKpiPoint> _collection;

    public KpiPointRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<InsightKpiPoint>("InsightsKpiPoints");
    }

    public async Task InsertManyAsync(IEnumerable<InsightKpiPoint> points, CancellationToken cancellationToken)
    {
        var list = points.ToList();
        if (list.Count == 0)
        {
            return;
        }

        try
        {
            await _collection.InsertManyAsync(list, new InsertManyOptions { IsOrdered = false }, cancellationToken);
        }
        catch (MongoBulkWriteException<InsightKpiPoint> ex) when (ex.WriteErrors.All(e => e.Category == ServerErrorCategory.DuplicateKey))
        {
            // idempotency: ignore duplicates
        }
    }

    public async Task<IReadOnlyList<InsightKpiPoint>> GetSeriesAsync(
        string appId,
        string env,
        string metric,
        string bucket,
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken cancellationToken)
    {
        var filter = Builders<InsightKpiPoint>.Filter.Eq(x => x.AppId, appId)
            & Builders<InsightKpiPoint>.Filter.Eq(x => x.Env, env)
            & Builders<InsightKpiPoint>.Filter.Eq(x => x.Metric, metric)
            & Builders<InsightKpiPoint>.Filter.Eq(x => x.Bucket, bucket)
            & Builders<InsightKpiPoint>.Filter.Gte(x => x.T, startUtc)
            & Builders<InsightKpiPoint>.Filter.Lte(x => x.T, endUtc);

        return await _collection.Find(filter)
            .SortBy(x => x.T)
            .ToListAsync(cancellationToken);
    }

    public async Task<InsightKpiPoint?> GetLatestAsync(
        string appId,
        string env,
        string metric,
        string bucket,
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken cancellationToken)
    {
        var filter = Builders<InsightKpiPoint>.Filter.Eq(x => x.AppId, appId)
            & Builders<InsightKpiPoint>.Filter.Eq(x => x.Env, env)
            & Builders<InsightKpiPoint>.Filter.Eq(x => x.Metric, metric)
            & Builders<InsightKpiPoint>.Filter.Eq(x => x.Bucket, bucket)
            & Builders<InsightKpiPoint>.Filter.Gte(x => x.T, startUtc)
            & Builders<InsightKpiPoint>.Filter.Lte(x => x.T, endUtc);

        return await _collection.Find(filter)
            .SortByDescending(x => x.T)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
