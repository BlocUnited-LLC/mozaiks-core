using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Insights.API.Health;

public sealed class MongoHealthCheck : IHealthCheck
{
    private readonly IMongoClient _client;

    public MongoHealthCheck(IMongoClient client)
    {
        _client = client;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.GetDatabase("admin")
                .RunCommandAsync((Command<BsonDocument>)"{ping:1}", cancellationToken: cancellationToken);

            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("MongoDB ping failed", ex);
        }
    }
}
