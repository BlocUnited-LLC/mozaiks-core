using Insights.API.Models;

namespace Insights.API.Repository;

public interface IKpiPointRepository
{
    Task InsertManyAsync(IEnumerable<InsightKpiPoint> points, CancellationToken cancellationToken);

    Task<IReadOnlyList<InsightKpiPoint>> GetSeriesAsync(
        string appId,
        string env,
        string metric,
        string bucket,
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken cancellationToken);

    Task<InsightKpiPoint?> GetLatestAsync(
        string appId,
        string env,
        string metric,
        string bucket,
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken cancellationToken);
}
