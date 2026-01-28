using Insights.API.Models;

namespace Insights.API.Repository;

public interface IApiKeyUsageRepository
{
    Task<ApiKeyUsageRecord?> GetByAppIdAsync(string appId, CancellationToken cancellationToken);

    Task UpsertKpiPushAsync(string appId, DateTime nowUtc, string? sdkVersion, CancellationToken cancellationToken);

    Task UpsertEventPushAsync(string appId, DateTime nowUtc, string? sdkVersion, CancellationToken cancellationToken);

    Task SetEventsLast24hAsync(string appId, int eventsLast24h, DateTime computedAtUtc, CancellationToken cancellationToken);
}

