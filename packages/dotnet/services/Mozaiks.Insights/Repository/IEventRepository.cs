using Insights.API.Models;

namespace Insights.API.Repository;

public interface IEventRepository
{
    Task InsertManyAsync(IEnumerable<InsightEvent> events, CancellationToken cancellationToken);

    Task<IReadOnlyList<InsightEvent>> GetLatestAsync(
        string appId,
        string env,
        DateTime startUtc,
        DateTime endUtc,
        int limit,
        CancellationToken cancellationToken);

    Task<long> CountSinceAsync(string appId, DateTime sinceUtc, CancellationToken cancellationToken);
}
