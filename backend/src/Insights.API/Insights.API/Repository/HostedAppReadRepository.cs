using Insights.API.Models;
using MongoDB.Driver;

namespace Insights.API.Repository;

public sealed class HostedAppReadRepository : IHostedAppReadRepository
{
    private readonly IMongoCollection<HostedApp> _collection;

    public HostedAppReadRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<HostedApp>("HostedApps");
    }

    public async Task<HostedApp?> GetByAppIdAsync(string appId, CancellationToken cancellationToken)
    {
        return await _collection.Find(x => x.AppId == appId)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
